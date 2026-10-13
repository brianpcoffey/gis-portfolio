/*
 * scoring_kernel.cpp
 *
 * Native implementation of the HomeFinder composite property-scoring kernel.
 * Mirrors HomeScoringService.cs sub-score methods exactly so that
 * NativeScoringBridgeTests can assert parity within floating-point tolerance.
 *
 * Key design choices:
 *  - No global mutable state; fully reentrant.
 *  - Both entry points validate their arguments against the caller-supplied
 *    output length, return a status code (see portfolio_scoring.h) and catch
 *    every C++ exception, so nothing is thrown across the C ABI.
 *  - All helpers are file-scoped (anonymous namespace) to avoid symbol
 *    pollution and allow aggressive inlining.
 *  - ScorePropertyBatch iterates a plain for-loop over contiguous structs,
 *    which leaves the compiler free to vectorise it under AVX2; measured,
 *    it is no faster than the managed path (root README.md, "Measured speedup").
 *  - LoanTermMonths and AnnualInterestRate are constexpr to match the C#
 *    constants and allow compile-time evaluation where possible.
 */

#include "portfolio_scoring.h"

#include <cmath>
#include <algorithm>  /* std::clamp */
#include <cstdint>

namespace
{
	constexpr double kAnnualInterestRate = 0.065;
	constexpr int    kLoanTermMonths     = 360;  // 30-year fixed

	inline double clamp100(double v) noexcept
	{
		return std::clamp(v, 0.0, 100.0);
	}

	/*
	 * int32 subtraction that wraps modulo 2^32, which is what unchecked C# int arithmetic does and
	 * therefore what the managed scoring path computes. Signed overflow is undefined behaviour in
	 * C++, so the subtraction is done on unsigned values (always defined) and converted back
	 * (modular since C++20). Inputs come over the ABI, so no range can be assumed.
	 */
	inline std::int32_t wrapping_sub(std::int32_t a, std::int32_t b) noexcept
	{
		return static_cast<std::int32_t>(static_cast<std::uint32_t>(a) - static_cast<std::uint32_t>(b));
	}

	/*
	 * EstimateMonthlyCost
	 * Matches HomeScoringService.EstimateMonthlyCost(Property p) exactly.
	 * Uses the standard amortisation formula:
	 *   M = P * r * (1+r)^n / ((1+r)^n - 1)
	 * where P = 80% of price, r = monthly rate, n = 360 months.
	 */
	inline double estimate_monthly_cost(const PropertyInputNative& p) noexcept
	{
		const double monthly_rate = kAnnualInterestRate / 12.0;
		const double loan_amount  = p.price * 0.80;

		double mortgage;
		if (monthly_rate > 0.0)
		{
			const double compound = std::pow(1.0 + monthly_rate, static_cast<double>(kLoanTermMonths));
			mortgage = loan_amount * (monthly_rate * compound / (compound - 1.0));
		}
		else
		{
			mortgage = loan_amount / static_cast<double>(kLoanTermMonths);
		}

		return std::round((mortgage + (p.propertyTax / 12.0) + p.hoaFee + p.utilities) * 100.0) / 100.0;
	}

	/*
	 * ScoreAffordability
	 * Returns 50 when budget <= 0 (matching C# guard).
	 */
	inline double score_affordability(double monthly_cost, double budget) noexcept
	{
		if (budget <= 0.0) return 50.0;
		const double ratio = monthly_cost / budget;
		return clamp100((1.0 - ratio) * 100.0);
	}

	/*
	 * ScoreNeighborhood
	 * Average of four 0-100 sub-scores; crime is inverted.
	 */
	inline double score_neighborhood(const PropertyInputNative& p) noexcept
	{
		return clamp100(
			(p.schoolRating + p.walkability + p.transitAccess + (100.0 - p.crimeScore))
			/ 4.0);
	}

	/*
	 * ScoreSize
	 * sqft ratio contributes up to 50 pts; extra bedrooms add 10 pts each.
	 */
	inline double score_size(const PropertyInputNative& p,
							 const PreferencesInputNative& prefs) noexcept
	{
		const double sqft_ratio   = (prefs.minSqft > 0)
									? (p.lotSqft / static_cast<double>(prefs.minSqft))
									: 1.0;
		const double bedroom_bonus = wrapping_sub(p.bedrooms, prefs.minBedrooms) * 10.0;
		return clamp100(std::min(sqft_ratio * 50.0, 100.0) + bedroom_bonus);
	}

	/*
	 * ScoreCondition
	 * Averages four system conditions (0-100) then adds a recency bonus.
	 * If lastRenovationYear == 0 the bonus is 0 (unknown / never renovated).
	 */
	inline double score_condition(const PropertyInputNative& p,
								  int current_year) noexcept
	{
		const double avg = (p.roofCondition + p.acCondition
						  + p.plumbingCondition + p.electricalCondition) / 4.0;

		double reno = 0.0;
		if (p.lastRenovationYear > 0)
		{
			const std::int32_t years_since = wrapping_sub(current_year, p.lastRenovationYear);
			reno = std::max<std::int32_t>(0, wrapping_sub(10, years_since)) * 2.0;
		}
		return clamp100(avg + reno);
	}

	/*
	 * ScoreCommute
	 * Linear decay; 50 when maxCommute <= 0.
	 */
	inline double score_commute(double commute_min, int max_commute) noexcept
	{
		if (max_commute <= 0) return 50.0;
		return clamp100((1.0 - commute_min / static_cast<double>(max_commute)) * 100.0);
	}

	/*
	 * ScoreTaxUtilities
	 * Annual burden of tax + utilities + HOA; scaled so $30,000/yr = 0 pts.
	 */
	inline double score_tax_utilities(const PropertyInputNative& p) noexcept
	{
		const double annual = p.propertyTax + (p.utilities * 12.0) + (p.hoaFee * 12.0);
		return clamp100(std::max(0.0, 100.0 - (annual / 300.0)));
	}

	/*
	 * ScoreEnvironment
	 * Averages inverted flood-risk and inverted noise scores.
	 */
	inline double score_environment(const PropertyInputNative& p) noexcept
	{
		return clamp100(((100.0 - p.floodRisk) + (100.0 - p.noiseLevel)) / 2.0);
	}

	/*
	 * compute_scores
	 * Core function called by both the single and batch entry points.
	 * Fills one ScoreOutputNative in-place.
	 *
	 * Deliberately not noexcept: nothing here throws today, but a noexcept function that did
	 * would call std::terminate and take the whole .NET process down, while the entry points'
	 * catch (...) turns the same failure into PS_ERR_EXCEPTION.
	 */
	void compute_scores(const PropertyInputNative&    p,
						const PreferencesInputNative& prefs,
						ScoreOutputNative&            out)
	{
		/* Normalise weights; guard against all-zero to match C# totalWeight guard */
		const double total_weight =
			prefs.wAffordability + prefs.wNeighborhood + prefs.wSize
			+ prefs.wAppreciation + prefs.wCondition   + prefs.wCommute
			+ prefs.wAmenities   + prefs.wTaxUtilities + prefs.wResale
			+ prefs.wEnvironment;

		const double denom = (total_weight > 0.0) ? total_weight : 1.0;

		const double monthly_cost = estimate_monthly_cost(p);

		out.estimatedMonthlyCost = monthly_cost;
		out.affordability  = score_affordability(monthly_cost, prefs.maxMonthlyBudget);
		out.neighborhood   = score_neighborhood(p);
		out.size           = score_size(p, prefs);
		out.appreciation   = clamp100(p.futureAppreciation);
		out.condition      = score_condition(p, prefs.currentYear);
		out.commute        = score_commute(p.commuteMin, prefs.maxCommuteMin);
		out.amenities      = clamp100(p.amenitiesScore);
		out.taxUtilities   = score_tax_utilities(p);
		out.resale         = clamp100(p.resalePotential);
		out.environment    = score_environment(p);

		out.composite = clamp100(
			(prefs.wAffordability / denom) * out.affordability
		  + (prefs.wNeighborhood  / denom) * out.neighborhood
		  + (prefs.wSize          / denom) * out.size
		  + (prefs.wAppreciation  / denom) * out.appreciation
		  + (prefs.wCondition     / denom) * out.condition
		  + (prefs.wCommute       / denom) * out.commute
		  + (prefs.wAmenities     / denom) * out.amenities
		  + (prefs.wTaxUtilities  / denom) * out.taxUtilities
		  + (prefs.wResale        / denom) * out.resale
		  + (prefs.wEnvironment   / denom) * out.environment);
	}

} /* anonymous namespace */

extern "C"
{
	PS_API int32_t ScoreProperty(
		const PropertyInputNative*    property,
		const PreferencesInputNative* prefs,
		ScoreOutputNative*            out,
		int32_t                       outputLength)
	{
		try
		{
			if (outputLength < 0 || !property || !prefs || !out)
				return PS_ERR_INVALID_ARGUMENT;
			if (outputLength < 1)
				return PS_ERR_BUFFER_TOO_SMALL;

			compute_scores(*property, *prefs, *out);
			return PS_OK;
		}
		catch (...)
		{
			return PS_ERR_EXCEPTION;
		}
	}

	PS_API int32_t ScorePropertyBatch(
		const PropertyInputNative*    properties,
		int32_t                       count,
		const PreferencesInputNative* prefs,
		ScoreOutputNative*            scores,
		int32_t                       outputLength)
	{
		try
		{
			if (count < 0 || outputLength < 0)
				return PS_ERR_INVALID_ARGUMENT;
			if (outputLength < count)
				return PS_ERR_BUFFER_TOO_SMALL;
			if (count == 0)
				return PS_OK; // nothing to read or write, so no pointer is needed
			if (!properties || !prefs || !scores)
				return PS_ERR_INVALID_ARGUMENT;

			/*
			 * Plain indexed loop over contiguous input/output arrays. Iterations are
			 * independent, the helpers inline and the arrays cannot alias, so the
			 * compiler may vectorise it; whether it does depends on compiler and flags.
			 */
			for (int32_t i = 0; i < count; ++i)
			{
				compute_scores(properties[i], *prefs, scores[i]);
			}
			return PS_OK;
		}
		catch (...)
		{
			return PS_ERR_EXCEPTION;
		}
	}

	PS_API int32_t Scoring_AbiVersion(void)
	{
		return PS_ABI_VERSION;
	}
}
