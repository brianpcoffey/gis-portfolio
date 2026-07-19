#pragma once

#if defined(_WIN32)
	#define VRP_API __declspec(dllexport)
#else
	#define VRP_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
// One customer stop in a capacitated vehicle routing problem with time windows.
struct VrpStopNative
{
	double demand;        // load consumed at this stop
	double readyTime;     // earliest service time, in minutes on the shift clock
	double dueTime;       // latest service time, in minutes on the shift clock
	double serviceTime;   // minutes spent at the stop, separate from travel
};
#pragma pack(pop)

extern "C"
{
	// Clarke-Wright parallel savings construction followed by first-improvement
	// 2-opt / Or-opt local search.
	//
	// costMatrix (kilometres) and travelTimeMatrix (minutes) are row-major
	// [matrixDim x matrixDim], where index 0 is the depot and index k+1 is stops[k].
	// matrixDim must equal stopCount + 1. Unreachable pairs carry INFINITY, which the
	// feasibility walk rejects naturally.
	//
	// Routes are returned using the flat-buffer idiom: route r occupies the next
	// outRouteLengths[r] entries of outRouteStops. Values are ZERO-BASED STOP INDICES
	// (not matrix indices, not node ids) — the depot is implicit at both ends.
	//
	// A stop that cannot be served by any single vehicle (demand above capacity, or a
	// window unreachable from the depot inside the shift) is simply omitted from every
	// route, as is any stop left over once maxVehicles routes are in use. Infeasibility
	// is therefore expressed as an unserved stop rather than an error status: the caller
	// diffs the emitted stop indices against 0..stopCount-1.
	//
	// outIterationCosts records the objective after construction and after each local
	// search pass, so the client can plot convergence. outIterationCount receives the
	// number of values written.
	//
	// Returns the number of routes used (>= 0), negative on error.
	VRP_API int Vrp_SolveCvrptw(
		const double* costMatrix,
		const double* travelTimeMatrix,
		int matrixDim,
		const VrpStopNative* stops,
		int stopCount,
		double vehicleCapacity,
		int maxVehicles,
		double shiftStartMinutes,
		double shiftEndMinutes,
		double vehicleFixedCost,
		int maxIterations,
		int* outRouteStops,
		int routeStopsCapacity,
		int* outRouteLengths,
		int routeLengthsCapacity,
		double* outIterationCosts,
		int iterationCapacity,
		int* outIterationCount);
}
