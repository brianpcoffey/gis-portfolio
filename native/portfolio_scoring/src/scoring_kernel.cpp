/*
 * scoring_kernel.cpp
 *
 * Native implementation of the HomeFinder composite property-scoring kernel.
 * Mirrors HomeScoringService.cs sub-score methods exactly so that
 * NativeScoringBridgeTests can assert parity within floating-point tolerance.
 *
 * Key design choices:
 *  - No global mutable state; fully reentrant.
 *  - All helpers are file-scoped (anonymous namespace) to avoid symbol
 *    pollution and allow aggressive inlining.
 *  - ScorePropertyBatch iterates a plain for-loop over contiguous structs.
 *    With /arch:AVX2 (MSVC) or -march=haswell (GCC/Clang) the compiler
 *    auto-vectorises the bulk of the arithmetic to 4-wide double SIMD lanes.
 *  - LoanTermMonths and AnnualInterestRate are constexpr to match the C#
 *    constants and allow compile-time evaluation where possible.
 */

#include "portfolio_scoring.h"

#include <cmath>
#include <algorithm>  /* std::clamp */
#include <cstdint>

/* -----------------------------------------------------------------------
 * Internal helpers (not exported)
 * ----------------------------------------------------------------------- */
namespace
{
	constexpr double kAnnualInterestRate = 0.065;
	constexpr int    kLoanTermMonths     = 360;  // 30-year fixed

	/* clamp to [0, 100] */
	inline double clamp100(double v) noexcept
	{
		return std::clamp(v, 0.0, 100.0);
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

		/* mortgage + annual_tax/12 + monthly_hoa + monthly_utilities */
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
		const double bedroom_bonus = (p.bedrooms - prefs.minBedrooms) * 10.0;
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
			const int years_since = current_year - p.lastRenovationYear;
			reno = std::max(0, 10 - years_since) * 2.0;
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
	 */
	void compute_scores(const PropertyInputNative&    p,
						const PreferencesInputNative& prefs,
						ScoreOutputNative&            out) noexcept
	{
		/* Normalise weights; guard against all-zero to match C# totalWeight guard */
		const double total_weight =
			prefs.wAffordability + prefs.wNeighborhood + prefs.wSize
			+ prefs.wAppreciation + prefs.wCondition   + prefs.wCommute
			+ prefs.wAmenities   + prefs.wTaxUtilities + prefs.wResale
			+ prefs.wEnvironment;

		const double denom = (total_weight > 0.0) ? total_weight : 1.0;

		/* Sub-scores */
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

		/* Weighted composite */
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

/* -----------------------------------------------------------------------
 * Exported entry points
 * ----------------------------------------------------------------------- */

extern "C"
{
	PS_API void ScoreProperty(
		const PropertyInputNative*    property,
		const PreferencesInputNative* prefs,
		ScoreOutputNative*            out)
	{
		compute_scores(*property, *prefs, *out);
	}

	PS_API void ScorePropertyBatch(
		const PropertyInputNative*    properties,
		int32_t                       count,
		const PreferencesInputNative* prefs,
		ScoreOutputNative*            scores)
	{
		/*
		 * Plain indexed loop over contiguous input/output arrays.
		 * The compiler can auto-vectorise this loop because:
		 *  - All sub-score functions are inlined.
		 *  - There are no data dependencies between iterations.
		 *  - Input and output arrays cannot alias (separate pointers).
		 *
		 * With /arch:AVX2 or -march=haswell -ffast-math the inner arithmetic
		 * on doubles maps to VFMADD231PD / VMULPD instructions.
		 */
		for (int32_t i = 0; i < count; ++i)
		{
			compute_scores(properties[i], *prefs, scores[i]);
		}
	}
}
