#pragma once

#if defined(_WIN32)
	#define CAT_API __declspec(dllexport)
#else
	#define CAT_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
// One insured location in a policy book.
struct CatLocationNative
{
	double latitude;
	double longitude;
	double insuredValue;    // TIV in dollars
	double siteHazard;      // 0..1 baseline susceptibility (terrain + fuel + WUI)
	double deductibleRate;  // fraction of TIV retained by the insured
	double limitRate;       // fraction of TIV that caps the payout
};

// One stochastic catastrophe event in the simulation catalog.
struct CatEventNative
{
	double latitude;        // epicenter
	double longitude;
	double intensity;       // 0..1 severity at the epicenter
	double radiusKm;        // footprint radius; intensity decays linearly to 0 at the edge
	double annualRate;      // Poisson frequency, events per year
};
#pragma pack(pop)

extern "C"
{
	// Ring accumulation: for each location, sums the insured value of every location
	// within radiusKm (including itself). Brute-force O(n^2) haversine — this is the
	// concentration-control workload the native path exists to accelerate.
	// Writes one total per location into `outRingTiv` (parallel to `locations`) and the
	// neighbour count into `outNeighborCount`.
	// Returns 0 on success, negative on error.
	CAT_API int Cat_ComputeRingAccumulation(
		const CatLocationNative* locations,
		int locationCount,
		double radiusKm,
		double* outRingTiv,
		int* outNeighborCount,
		int outputLength);

	// Monte Carlo event loss simulation. For each event e and location l:
	//   d        = haversine(event, location)
	//   siteInt  = event.intensity * max(0, 1 - d / event.radiusKm) * location.siteHazard
	//   mdr      = 1 - exp(-vulnerabilityAlpha * siteInt)
	//   groundUp = location.insuredValue * mdr
	//   gross    = clamp(groundUp - TIV * deductibleRate, 0, TIV * limitRate)
	// `outEventLosses[e]` receives the summed gross loss across all locations for event e
	// and `outAffectedCounts[e]` the number of locations taking a non-zero loss.
	// Returns 0 on success, negative on error.
	CAT_API int Cat_SimulateEventLosses(
		const CatLocationNative* locations,
		int locationCount,
		const CatEventNative* events,
		int eventCount,
		double vulnerabilityAlpha,
		double* outEventLosses,
		int* outAffectedCounts,
		int outputLength);
}
