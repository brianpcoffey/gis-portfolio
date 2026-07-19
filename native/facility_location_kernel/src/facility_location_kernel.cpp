#include "facility_location_kernel.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <vector>

namespace
{
	constexpr double kInfinity = std::numeric_limits<double>::infinity();

	// An improvement must beat the incumbent by more than this to be accepted. Without a
	// tolerance, floating-point noise lets the substitution loop oscillate between two
	// equivalent configurations until maxIterations is exhausted.
	constexpr double kImprovementEpsilon = 1e-12;

	// Orders demand points by response time, breaking ties on index so the ordering is a
	// total order. Both the native kernel and the managed fallback sort with this exact
	// comparator, which is what makes their weighted percentiles bit-identical.
	struct ResponseOrder
	{
		const double* distances;

		bool operator()(int a, int b) const
		{
			const double da = distances[a];
			const double db = distances[b];
			if (da < db) return true;
			if (db < da) return false;
			return a < b;
		}
	};

	// Demand-weighted percentile. Walks the response-time ordering accumulating weight and
	// returns the response time at which cumulative weight first reaches `fraction` of the
	// total. This is NOT the percentile of the unweighted list: one heavily weighted
	// neighbourhood can set the p90 on its own, which is exactly the behaviour NFPA 1710
	// is written to capture.
	double weighted_percentile(
		const double* distances,
		const double* weights,
		const int* order,
		int count,
		double totalWeight,
		double fraction)
	{
		if (count <= 0 || !(totalWeight > 0.0))
			return 0.0;

		const double target = totalWeight * fraction;
		double cumulative = 0.0;
		for (int i = 0; i < count; ++i)
		{
			const int d = order[i];
			cumulative += weights[d];
			if (cumulative >= target)
				return distances[d];
		}

		return distances[order[count - 1]];
	}

	// Scores an assignment. Lower is better in every mode, so the search treats them
	// uniformly.
	double evaluate_objective(
		const double* distances,
		const double* weights,
		int count,
		double totalWeight,
		int objectiveMode,
		double coverageThreshold,
		std::vector<int>& orderScratch)
	{
		if (count <= 0 || !(totalWeight > 0.0))
			return 0.0;

		if (objectiveMode == 0)
		{
			double sum = 0.0;
			for (int d = 0; d < count; ++d)
				sum += weights[d] * distances[d];
			return sum / totalWeight;
		}

		if (objectiveMode == 2)
		{
			double uncovered = 0.0;
			for (int d = 0; d < count; ++d)
			{
				if (!(distances[d] <= coverageThreshold))
					uncovered += weights[d];
			}
			return uncovered / totalWeight;
		}

		for (int d = 0; d < count; ++d)
			orderScratch[d] = d;
		std::sort(orderScratch.begin(), orderScratch.begin() + count, ResponseOrder{ distances });
		return weighted_percentile(distances, weights, orderScratch.data(), count, totalWeight, 0.90);
	}

	// Recomputes the nearest and second-nearest open facility for every demand point.
	// nearestSlot holds the POSITION within `chosen`, not the candidate index, because the
	// substitution loop removes facilities by position.
	void rebuild_nearest(
		const double* costMatrix,
		int demandCount,
		const std::vector<int>& chosen,
		std::vector<double>& nearest,
		std::vector<int>& nearestSlot,
		std::vector<double>& second)
	{
		const int p = static_cast<int>(chosen.size());
		for (int d = 0; d < demandCount; ++d)
		{
			nearest[d] = kInfinity;
			second[d] = kInfinity;
			nearestSlot[d] = -1;
		}

		for (int slot = 0; slot < p; ++slot)
		{
			const double* row = costMatrix + static_cast<size_t>(chosen[slot]) * demandCount;
			for (int d = 0; d < demandCount; ++d)
			{
				const double cost = row[d];
				if (cost < nearest[d])
				{
					second[d] = nearest[d];
					nearest[d] = cost;
					nearestSlot[d] = slot;
				}
				else if (cost < second[d])
				{
					second[d] = cost;
				}
			}
		}
	}
}

extern "C" FACLOC_API int Facility_SolvePMedian(
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
	int* outIterationCount)
{
	try
	{
		if (!costMatrix || !demandWeights || !outChosenCandidates || !outIterationObjectives || !outIterationCount)
			return -1;
		if (candidateCount <= 0 || demandCount <= 0)
			return -1;
		if (facilityCount <= 0 || facilityCount > candidateCount)
			return -1;
		if (objectiveMode < 0 || objectiveMode > 2)
			return -1;
		if (maxIterations < 0)
			return -1;
		if (chosenCapacity < facilityCount || iterationCapacity < 1)
			return -3;

		*outIterationCount = 0;

		double totalWeight = 0.0;
		for (int d = 0; d < demandCount; ++d)
		{
			if (!(demandWeights[d] > 0.0))
				return -1;
			totalWeight += demandWeights[d];
		}

		std::vector<int> orderScratch(demandCount);
		std::vector<double> nearest(demandCount, kInfinity);
		std::vector<double> second(demandCount, kInfinity);
		std::vector<int> nearestSlot(demandCount, -1);
		std::vector<double> trial(demandCount, kInfinity);
		std::vector<char> isChosen(candidateCount, 0);
		std::vector<int> chosen;
		chosen.reserve(facilityCount);

		// ── Greedy seed ────────────────────────────────────────────────────────
		// Open the single best facility, then repeatedly open whichever remaining
		// candidate improves the objective most given everything already open.
		for (int k = 0; k < facilityCount; ++k)
		{
			int best = -1;
			double bestObjective = kInfinity;

			for (int c = 0; c < candidateCount; ++c)
			{
				if (isChosen[c])
					continue;

				const double* row = costMatrix + static_cast<size_t>(c) * demandCount;
				for (int d = 0; d < demandCount; ++d)
					trial[d] = row[d] < nearest[d] ? row[d] : nearest[d];

				const double objective = evaluate_objective(
					trial.data(), demandWeights, demandCount, totalWeight,
					objectiveMode, coverageThreshold, orderScratch);

				if (best < 0 || objective < bestObjective - kImprovementEpsilon)
				{
					bestObjective = objective;
					best = c;
				}
			}

			if (best < 0)
				break;

			isChosen[best] = 1;
			chosen.push_back(best);

			const double* row = costMatrix + static_cast<size_t>(best) * demandCount;
			for (int d = 0; d < demandCount; ++d)
			{
				if (row[d] < nearest[d])
					nearest[d] = row[d];
			}
		}

		if (chosen.empty())
			return -1;

		rebuild_nearest(costMatrix, demandCount, chosen, nearest, nearestSlot, second);

		double currentObjective = evaluate_objective(
			nearest.data(), demandWeights, demandCount, totalWeight,
			objectiveMode, coverageThreshold, orderScratch);

		outIterationObjectives[0] = currentObjective;
		*outIterationCount = 1;

		// ── Teitz-Bart vertex substitution ─────────────────────────────────────
		// Each pass evaluates every (open facility, closed candidate) swap and applies
		// the single best improving one. Because nearest and second-nearest are cached,
		// a trial costs O(demand) instead of O(demand x p): dropping facility `slot`
		// leaves a demand point on its second-nearest, and everything else keeps its
		// current nearest, so one pass over the candidate's row finishes the job.
		const int p = static_cast<int>(chosen.size());
		for (int iteration = 0; iteration < maxIterations && p < candidateCount; ++iteration)
		{
			int bestSlot = -1;
			int bestCandidate = -1;
			double bestObjective = currentObjective;

			for (int slot = 0; slot < p; ++slot)
			{
				for (int c = 0; c < candidateCount; ++c)
				{
					if (isChosen[c])
						continue;

					const double* row = costMatrix + static_cast<size_t>(c) * demandCount;
					for (int d = 0; d < demandCount; ++d)
					{
						const double base = nearestSlot[d] == slot ? second[d] : nearest[d];
						trial[d] = row[d] < base ? row[d] : base;
					}

					const double objective = evaluate_objective(
						trial.data(), demandWeights, demandCount, totalWeight,
						objectiveMode, coverageThreshold, orderScratch);

					if (objective < bestObjective - kImprovementEpsilon)
					{
						bestObjective = objective;
						bestSlot = slot;
						bestCandidate = c;
					}
				}
			}

			if (bestSlot < 0)
				break;

			isChosen[chosen[bestSlot]] = 0;
			isChosen[bestCandidate] = 1;
			chosen[bestSlot] = bestCandidate;

			rebuild_nearest(costMatrix, demandCount, chosen, nearest, nearestSlot, second);
			currentObjective = bestObjective;

			if (*outIterationCount < iterationCapacity)
			{
				outIterationObjectives[*outIterationCount] = currentObjective;
				++(*outIterationCount);
			}
		}

		std::sort(chosen.begin(), chosen.end());
		for (int i = 0; i < static_cast<int>(chosen.size()); ++i)
			outChosenCandidates[i] = chosen[i];

		return static_cast<int>(chosen.size());
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" FACLOC_API int Facility_EvaluateCoverage(
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
	double secondThreshold)
{
	try
	{
		if (!costMatrix || !demandWeights || !chosenCandidates || !outAssignment || !outResponseTimes)
			return -1;
		if (!outMean || !outP50 || !outP90 || !outPctWithinFirstThreshold || !outPctWithinSecondThreshold)
			return -1;
		if (candidateCount <= 0 || demandCount <= 0 || chosenCount <= 0)
			return -1;
		if (outputCapacity < demandCount)
			return -3;

		for (int i = 0; i < chosenCount; ++i)
		{
			if (chosenCandidates[i] < 0 || chosenCandidates[i] >= candidateCount)
				return -2;
		}

		double totalWeight = 0.0;
		for (int d = 0; d < demandCount; ++d)
		{
			if (!(demandWeights[d] > 0.0))
				return -1;
			totalWeight += demandWeights[d];
		}

		for (int d = 0; d < demandCount; ++d)
		{
			outResponseTimes[d] = kInfinity;
			outAssignment[d] = -1;
		}

		for (int i = 0; i < chosenCount; ++i)
		{
			const int candidate = chosenCandidates[i];
			const double* row = costMatrix + static_cast<size_t>(candidate) * demandCount;
			for (int d = 0; d < demandCount; ++d)
			{
				if (row[d] < outResponseTimes[d])
				{
					outResponseTimes[d] = row[d];
					outAssignment[d] = candidate;
				}
			}
		}

		double weightedSum = 0.0;
		double withinFirst = 0.0;
		double withinSecond = 0.0;
		for (int d = 0; d < demandCount; ++d)
		{
			weightedSum += demandWeights[d] * outResponseTimes[d];
			if (outResponseTimes[d] <= firstThreshold)
				withinFirst += demandWeights[d];
			if (outResponseTimes[d] <= secondThreshold)
				withinSecond += demandWeights[d];
		}

		std::vector<int> order(demandCount);
		for (int d = 0; d < demandCount; ++d)
			order[d] = d;
		std::sort(order.begin(), order.end(), ResponseOrder{ outResponseTimes });

		*outMean = weightedSum / totalWeight;
		*outP50 = weighted_percentile(outResponseTimes, demandWeights, order.data(), demandCount, totalWeight, 0.50);
		*outP90 = weighted_percentile(outResponseTimes, demandWeights, order.data(), demandCount, totalWeight, 0.90);
		*outPctWithinFirstThreshold = 100.0 * withinFirst / totalWeight;
		*outPctWithinSecondThreshold = 100.0 * withinSecond / totalWeight;

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
