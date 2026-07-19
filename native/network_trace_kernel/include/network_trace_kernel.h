#pragma once

#if defined(_WIN32)
	#define TRACE_API __declspec(dllexport)
#else
	#define TRACE_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
// One element of an electric distribution circuit: a conductor span, a protective
// device, a switch, or a service transformer.
struct TraceElementNative
{
	int id;
	int fromNodeId;      // upstream-side node under normal (radial) flow
	int toNodeId;        // downstream-side node
	int deviceType;      // 0=conductor 1=switch 2=fuse 3=recloser 4=breaker 5=tieSwitch 6=transformer
	int isOpen;          // 1 = open (no current flows through this element)
	int customerCount;   // customers fed directly by this element
};
#pragma pack(pop)

extern "C"
{
	// Everything electrically downstream of (and including) the faulted element,
	// stopping at open devices. Writes element ids and the total customers affected.
	// Returns the number of ids written (>= 0), negative on error.
	TRACE_API int Trace_Downstream(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		int faultElementId,
		int* outElementIds, int outputCapacity,
		int* outCustomersAffected);

	// The ordered path from the faulted element back to the source node, faulted
	// element first and the element incident on the source last.
	// Returns the number of ids written (>= 0), negative on error.
	TRACE_API int Trace_Upstream(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		int faultElementId,
		int* outElementIds, int outputCapacity);

	// The nearest upstream protective device (breaker/recloser/fuse) whose opening
	// isolates the faulted element from the source, plus every closed switch that
	// bounds the de-energized section on the downstream side.
	// Returns the number of ids written (>= 0), negative on error.
	TRACE_API int Trace_FindIsolationDevices(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		int faultElementId,
		int* outDeviceIds, int outputCapacity);

	// Connectivity sweep from the source with a set of device-state overrides applied.
	// Used to evaluate a proposed switching plan: which elements stay energized, and
	// how many customers are served.
	//
	// The sweep is deliberately UNDIRECTED. fromNodeId/toNodeId record the nominal
	// flow direction for upstream/downstream semantics, but energization is pure
	// connectivity — a closed tie switch backfeeds against the nominal direction.
	//
	// Returns the number of energized element ids written (>= 0), negative on error.
	TRACE_API int Trace_ComputeEnergizedSet(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		const int* overrideElementIds,
		const int* overrideStates,      // 1 = open, 0 = closed
		int overrideCount,
		int* outEnergizedElementIds, int outputCapacity,
		int* outCustomersServed);
}
