#pragma once

/*
 * portfolio_scoring.h
 *
 * Public C ABI for the HomeFinder property scoring kernel.
 * All structs are tightly-packed sequences of doubles/ints so the layout
 * matches the blittable .NET interop structs in Portfolio.Services/Native/
 * without any marshalling copies.
 *
 * Thread-safety: ScoreProperty and ScorePropertyBatch are fully reentrant
 * (no global state).  Safe to call from any thread, including the .NET
 * thread-pool workers used by the Channel-based batch pipeline.
 */

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* -----------------------------------------------------------------------
 * Input / output structs
 * All fields match the corresponding C# properties in Property.cs and
 * HomeSearchPreferencesDto.cs.  Field order MUST stay in sync with the
 * [StructLayout(LayoutKind.Sequential)] definitions in NativeStructs.cs.
 * ----------------------------------------------------------------------- */

#pragma pack(push, 8)

typedef struct PropertyInputNative
{
	/* Financial */
	double price;
	double propertyTax;       /* annual, USD */
	double hoaFee;            /* monthly, USD */
	double utilities;         /* monthly, USD */

	/* Size */
	double lotSqft;
	int32_t bedrooms;
	double  bathrooms;

	/* Commute */
	double commuteMin;

	/* Neighbourhood (0-100 integer scores promoted to double) */
	double schoolRating;
	double walkability;
	double transitAccess;
	double crimeScore;

	/* Investment */
	double futureAppreciation;
	double resalePotential;

	/* Condition (0-100) */
	double roofCondition;
	double acCondition;
	double plumbingCondition;
	double electricalCondition;
	int32_t lastRenovationYear; /* 0 = unknown */

	/* Amenities / misc */
	double amenitiesScore;

	/* Environmental (0-100) */
	double floodRisk;
	double noiseLevel;

	/* GIS */
	double latitude;
	double longitude;
} PropertyInputNative;

typedef struct PreferencesInputNative
{
	/* Hard-filter thresholds (mirrors HomeSearchPreferencesDto) */
	double  maxPrice;
	double  maxMonthlyBudget;
	int32_t minBedrooms;
	double  minSqft;
	int32_t maxCommuteMin;

	/* Importance weights (raw; kernel normalises to sum=1 internally) */
	double wAffordability;
	double wNeighborhood;
	double wSize;
	double wAppreciation;
	double wCondition;
	double wCommute;
	double wAmenities;
	double wTaxUtilities;
	double wResale;
	double wEnvironment;

	/* Current UTC year – passed in so the kernel is deterministic in tests */
	int32_t currentYear;
} PreferencesInputNative;

typedef struct ScoreOutputNative
{
	double affordability;
	double neighborhood;
	double size;
	double appreciation;
	double condition;
	double commute;
	double amenities;
	double taxUtilities;
	double resale;
	double environment;
	double composite;           /* weighted sum, clamped to [0, 100] */
	double estimatedMonthlyCost;
} ScoreOutputNative;

#pragma pack(pop)

/* -----------------------------------------------------------------------
 * Exported functions
 * ----------------------------------------------------------------------- */

#if defined(_WIN32) || defined(_WIN64)
#  define PS_API __declspec(dllexport)
#else
#  define PS_API __attribute__((visibility("default")))
#endif

/*
 * ScoreProperty – score a single property.
 * Writes one ScoreOutputNative record to *out.
 */
PS_API void ScoreProperty(
	const PropertyInputNative*    property,
	const PreferencesInputNative* prefs,
	ScoreOutputNative*            out);

/*
 * ScorePropertyBatch – score `count` properties in one call.
 * `properties` and `scores` must each point to arrays of at least `count`
 * elements.  The same `prefs` pointer is shared across all rows.
 *
 * Designed so that the compiler can auto-vectorise the inner loop with
 * AVX2 (MSVC /arch:AVX2 or GCC/Clang -march=haswell -ffast-math).
 */
PS_API void ScorePropertyBatch(
	const PropertyInputNative*    properties,
	int32_t                       count,
	const PreferencesInputNative* prefs,
	ScoreOutputNative*            scores);

#ifdef __cplusplus
} /* extern "C" */
#endif
