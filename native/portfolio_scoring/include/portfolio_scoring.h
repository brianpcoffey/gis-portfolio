#pragma once

/*
 * portfolio_scoring.h
 *
 * Public C ABI for the HomeFinder property scoring kernel.
 * All structs are tightly-packed sequences of doubles/ints so the layout
 * matches the blittable .NET interop structs in Portfolio.Services/Native/
 * without any marshalling copies.
 *
 * The structs are mirrored by NativeStructs.cs, and the entry points are
 * declared in NativeScoringInterop.cs. Change the header, those two files, the
 * source and the parity tests together.
 *
 * Ownership and lifetime
 *   - The caller allocates every buffer (inputs, preferences, outputs) and keeps
 *     ownership of all of them. The kernel never allocates memory that crosses
 *     the boundary, never frees caller memory and never keeps a pointer after
 *     the call returns.
 *   - Every output pointer is followed by the number of elements it can hold
 *     (`outputLength`); the kernel writes nothing past it.
 *
 * Thread-safety: ScoreProperty and ScorePropertyBatch are fully reentrant
 * (no global state), so they are safe to call from any thread, as long as
 * concurrent calls do not share an output buffer.
 *
 * Errors: both entry points return a status code (below). No C++ exception
 * crosses the boundary: anything thrown is caught and returned as
 * PS_ERR_EXCEPTION.
 */

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/*
 * All fields match the corresponding C# properties in Property.cs and
 * HomeSearchPreferencesDto.cs.  Field order MUST stay in sync with the
 * [StructLayout(LayoutKind.Sequential)] definitions in NativeStructs.cs.
 */

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

#if defined(_WIN32) || defined(_WIN64)
#  define PS_API __declspec(dllexport)
#else
#  define PS_API __attribute__((visibility("default")))
#endif

/*
 * Version of this library's exported C ABI. The managed bridge calls Scoring_AbiVersion before anything
 * else and does not use a library that reports another version or has no such export, so a library built
 * before an ABI change is never called with the new argument list (a call made against the wrong
 * signature takes whatever is left in the registers for pointers and lengths).
 *   (no export)  ScoreProperty and ScorePropertyBatch returned void and took no outputLength
 *   2            both take outputLength after their output pointer and return a status code
 * Bump it whenever an exported signature or struct layout changes, together with
 * NativeScoringInterop.ExpectedAbiVersion in Portfolio.Services/Native.
 */
#define PS_ABI_VERSION 2

/* Returns PS_ABI_VERSION. Takes no arguments and touches no memory. */
PS_API int32_t Scoring_AbiVersion(void);

/*
 * Status codes returned by ScoreProperty and ScorePropertyBatch
 * (docs/agent-context.md section 5; -2, "not found", does not apply here).
 *
 *    0  PS_OK                    success
 *   -1  PS_ERR_INVALID_ARGUMENT  a null pointer that is needed, or a negative count or length
 *   -3  PS_ERR_BUFFER_TOO_SMALL  outputLength is smaller than the number of records to write
 *  -99  PS_ERR_EXCEPTION         a C++ exception was caught at the boundary
 *
 * The scores themselves cannot fail: every input value is accepted (there is no integer
 * division, and integer subtraction wraps like unchecked C#), so a non-zero status always
 * describes the call, not the data.
 *
 * A call rejected with -1 or -3 writes nothing. After -99 the contents of the output buffer
 * are unspecified.
 */
#define PS_OK                    0
#define PS_ERR_INVALID_ARGUMENT  (-1)
#define PS_ERR_BUFFER_TOO_SMALL  (-3)
#define PS_ERR_EXCEPTION         (-99)

/*
 * ScoreProperty – score a single property.
 *
 * Parameters
 *   property      in   one PropertyInputNative, read only; must not be null.
 *   prefs         in   one PreferencesInputNative, read only; must not be null.
 *   out           out  caller-allocated buffer of `outputLength` ScoreOutputNative records;
 *                      record 0 is written; must not be null.
 *   outputLength  in   capacity of `out` in records; must be >= 1 (0 is
 *                      PS_ERR_BUFFER_TOO_SMALL, negative is PS_ERR_INVALID_ARGUMENT).
 *
 * Returns a status code; 0 on success.
 */
PS_API int32_t ScoreProperty(
	const PropertyInputNative*    property,
	const PreferencesInputNative* prefs,
	ScoreOutputNative*            out,
	int32_t                       outputLength);

/*
 * ScorePropertyBatch – score `count` properties in one call.
 *
 * Parameters
 *   properties    in   `count` PropertyInputNative records, read only.
 *   count         in   number of properties; >= 0. Zero succeeds without touching any pointer
 *                      (they may be null).
 *   prefs         in   one PreferencesInputNative, read only, shared by every row.
 *   scores        out  caller-allocated buffer of `outputLength` ScoreOutputNative records; the
 *                      first `count` are written, in input order.
 *   outputLength  in   capacity of `scores` in records; must be >= count.
 *
 * Returns a status code; 0 on success.
 *
 * A plain loop over contiguous structs; the build enables AVX2, so the compiler
 * may vectorise it. Measured, it is no faster than the managed path
 * (native/README.md).
 */
PS_API int32_t ScorePropertyBatch(
	const PropertyInputNative*    properties,
	int32_t                       count,
	const PreferencesInputNative* prefs,
	ScoreOutputNative*            scores,
	int32_t                       outputLength);

#ifdef __cplusplus
} /* extern "C" */
#endif
