#pragma once

#if defined(_WIN32)
	#define FACLOC_API __declspec(dllexport)
#else
	#define FACLOC_API __attribute__((visibility("default")))
#endif

extern "C"
{
	// p-median / max-coverage facility location via greedy seeding plus Teitz-Bart
	// vertex substitution.
	//
	// costMatrix is row-major [candidateCount x demandCount], travel time in minutes.
	// costMatrix[c * demandCount + d] is the travel time from candidate c to demand d.
	//
	// objectiveMode: 0 = weighted mean response time
	//                1 = weighted 90th-percentile response time
	//                2 = fraction of demand NOT covered within coverageThreshold (MCLP)
	//
	// Lower objective is always better, including mode 2.
	//
	// outChosenCandidates receives the chosen candidate indices in ascending order.
	// outIterationObjectives records the objective after the greedy seed and after each
	// substitution pass, so the client can plot convergence; outIterationCount receives
	// the number of entries written.
	//
	// Returns the number of facilities chosen (>= 0), negative on error.
	FACLOC_API int Facility_SolvePMedian(
		const double* costMatrix,
		int candidateCount,
		int demandCount,
		const double* demandWeights,
		int facilityCount,
		int objectiveMode,
		double coverageThreshold,
		int maxIterations,
		int* outChosenCandidates, int chosenCapacity,
		double* outIterationObjectives, int iterationCapacity,
		int* outIterationCount);

	// Evaluates a fixed configuration: nearest-facility assignment and the resulting
	// response-time distribution.
	//
	// outAssignment and outResponseTimes are parallel to the demand array. outAssignment
	// receives the CANDIDATE INDEX (an index into the candidate axis of costMatrix), not
	// the position within chosenCandidates, and -1 where no facility can reach the point.
	//
	// The percentile outputs are demand-weighted: the demand points are ordered by
	// response time, weight is accumulated, and the reported value is the response time
	// at which cumulative weight first crosses the target fraction of total weight.
	//
	// Returns 0 on success, negative on error.
	FACLOC_API int Facility_EvaluateCoverage(
		const double* costMatrix,
		int candidateCount,
		int demandCount,
		const double* demandWeights,
		const int* chosenCandidates,
		int chosenCount,
		int* outAssignment,
		double* outResponseTimes,
		int outputCapacity,
		double* outMean,
		double* outP50,
		double* outP90,
		double* outPctWithinFirstThreshold,
		double* outPctWithinSecondThreshold,
		double firstThreshold,
		double secondThreshold);
}
