#include "vrp_solver_kernel.h"

#include <algorithm>
#include <cmath>
#include <vector>

namespace
{
	// Improvements smaller than this are treated as noise rather than progress, so the
	// search terminates instead of chasing floating-point dust.
	constexpr double kEpsilon = 1e-9;

	// Everything the objective and the feasibility walk need, gathered once so the move
	// operators stay short. Mirrored by FleetRoutingService.SolverContext on the managed
	// path — the two implementations must agree move for move.
	struct Solver
	{
		const double* cost;
		const double* time;
		int dim;
		const VrpStopNative* stops;
		int stopCount;
		double capacity;
		double shiftStart;
		double shiftEnd;
		double fixedCost;

		// Matrix index of a zero-based stop index. The depot occupies row/column 0.
		int mi(int stop) const { return stop + 1; }

		double leg(int fromMatrixIndex, int toMatrixIndex) const
		{
			return cost[fromMatrixIndex * dim + toMatrixIndex];
		}

		double legTime(int fromMatrixIndex, int toMatrixIndex) const
		{
			return time[fromMatrixIndex * dim + toMatrixIndex];
		}

		// Depot -> stops -> depot, in kilometres.
		double routeDistance(const std::vector<int>& route) const
		{
			if (route.empty())
				return 0.0;

			double total = leg(0, mi(route[0]));
			for (std::size_t k = 1; k < route.size(); ++k)
				total += leg(mi(route[k - 1]), mi(route[k]));
			total += leg(mi(route.back()), 0);
			return total;
		}

		double routeLoad(const std::vector<int>& route) const
		{
			double load = 0.0;
			for (std::size_t k = 0; k < route.size(); ++k)
				load += stops[route[k]].demand;
			return load;
		}

		// Walks the route on the shift clock. Early arrival waits; late arrival fails.
		// An unreachable leg carries INFINITY, which fails the due-time test.
		bool routeFeasible(const std::vector<int>& route) const
		{
			if (route.empty())
				return true;
			if (routeLoad(route) > capacity + kEpsilon)
				return false;

			double t = shiftStart;
			int prev = 0;
			for (std::size_t k = 0; k < route.size(); ++k)
			{
				const int stop = route[k];
				t += legTime(prev, mi(stop));
				if (!std::isfinite(t))
					return false;
				if (t < stops[stop].readyTime)
					t = stops[stop].readyTime;
				if (t > stops[stop].dueTime + kEpsilon)
					return false;
				t += stops[stop].serviceTime;
				prev = mi(stop);
			}

			t += legTime(prev, 0);
			return std::isfinite(t) && t <= shiftEnd + kEpsilon;
		}

		// Total travel distance plus a fixed penalty per vehicle used. The penalty is what
		// makes the search prefer four trucks over five.
		double objective(const std::vector<std::vector<int>>& routes) const
		{
			double total = 0.0;
			for (std::size_t r = 0; r < routes.size(); ++r)
			{
				if (routes[r].empty())
					continue;
				total += routeDistance(routes[r]) + fixedCost;
			}
			return total;
		}
	};

	struct Saving
	{
		double value;
		int i;
		int j;
	};

	void dropEmptyRoutes(std::vector<std::vector<int>>& routes)
	{
		std::vector<std::vector<int>> kept;
		kept.reserve(routes.size());
		for (std::size_t r = 0; r < routes.size(); ++r)
		{
			if (!routes[r].empty())
				kept.push_back(routes[r]);
		}
		routes.swap(kept);
	}

	// Phase 1 — Clarke-Wright parallel savings. One route per servable stop, then merge
	// route ends in descending savings order while capacity and every window still hold.
	std::vector<std::vector<int>> constructSavings(const Solver& s)
	{
		std::vector<std::vector<int>> routes;
		std::vector<int> routeOf(static_cast<std::size_t>(s.stopCount), -1);

		for (int i = 0; i < s.stopCount; ++i)
		{
			std::vector<int> single(1, i);
			if (!s.routeFeasible(single))
				continue;
			routeOf[static_cast<std::size_t>(i)] = static_cast<int>(routes.size());
			routes.push_back(single);
		}

		std::vector<Saving> savings;
		savings.reserve(static_cast<std::size_t>(s.stopCount) * static_cast<std::size_t>(s.stopCount));
		for (int i = 0; i < s.stopCount; ++i)
		{
			if (routeOf[static_cast<std::size_t>(i)] < 0)
				continue;
			for (int j = i + 1; j < s.stopCount; ++j)
			{
				if (routeOf[static_cast<std::size_t>(j)] < 0)
					continue;
				const double value = s.leg(0, s.mi(i)) + s.leg(0, s.mi(j)) - s.leg(s.mi(i), s.mi(j));
				if (!std::isfinite(value))
					continue;
				savings.push_back(Saving{ value, i, j });
			}
		}

		// Descending savings, with the index pair as a total tiebreak so the merge order —
		// and therefore the whole solution — is deterministic.
		std::sort(savings.begin(), savings.end(), [](const Saving& a, const Saving& b)
			{
				if (a.value != b.value)
					return a.value > b.value;
				if (a.i != b.i)
					return a.i < b.i;
				return a.j < b.j;
			});

		for (std::size_t k = 0; k < savings.size(); ++k)
		{
			const int i = savings[k].i;
			const int j = savings[k].j;
			const int ri = routeOf[static_cast<std::size_t>(i)];
			const int rj = routeOf[static_cast<std::size_t>(j)];
			if (ri < 0 || rj < 0 || ri == rj)
				continue;

			const std::vector<int>& a = routes[static_cast<std::size_t>(ri)];
			const std::vector<int>& b = routes[static_cast<std::size_t>(rj)];
			if (a.empty() || b.empty())
				continue;
			if (s.routeLoad(a) + s.routeLoad(b) > s.capacity + kEpsilon)
				continue;

			std::vector<int> merged;
			bool ok = false;

			// Only end-to-end joins are considered: reversing a leg is not free once time
			// windows are involved.
			if (a.back() == i && b.front() == j)
			{
				merged = a;
				merged.insert(merged.end(), b.begin(), b.end());
				ok = s.routeFeasible(merged);
			}
			if (!ok && b.back() == j && a.front() == i)
			{
				merged = b;
				merged.insert(merged.end(), a.begin(), a.end());
				ok = s.routeFeasible(merged);
			}
			if (!ok)
				continue;

			routes[static_cast<std::size_t>(ri)] = merged;
			routes[static_cast<std::size_t>(rj)].clear();
			for (std::size_t m = 0; m < merged.size(); ++m)
				routeOf[static_cast<std::size_t>(merged[m])] = ri;
		}

		dropEmptyRoutes(routes);
		return routes;
	}

	// More routes than trucks: keep the ones that serve the most stops, breaking ties on the
	// shorter route and then on stop index. The remainder become unserved. This runs after
	// local search, not before it — Or-opt consolidates routes, so truncating at construction
	// time would strand stops the search was about to absorb.
	void truncateToFleet(const Solver& s, std::vector<std::vector<int>>& routes, int maxVehicles)
	{
		if (static_cast<int>(routes.size()) <= maxVehicles)
			return;

		std::sort(routes.begin(), routes.end(), [&s](const std::vector<int>& x, const std::vector<int>& y)
			{
				if (x.size() != y.size())
					return x.size() > y.size();
				const double dx = s.routeDistance(x);
				const double dy = s.routeDistance(y);
				if (dx != dy)
					return dx < dy;
				return x[0] < y[0];
			});
		routes.resize(static_cast<std::size_t>(maxVehicles));
	}

	// 2-opt within one route: reverse route[i..j] and keep the reversal when it shortens
	// the route and stays time-feasible. First improvement, restarting after each move.
	bool twoOptPass(const Solver& s, std::vector<std::vector<int>>& routes)
	{
		bool anyImproved = false;

		// One scratch buffer for the whole pass. `assign` reuses its capacity, so the
		// candidate loop below runs allocation-free after the first move.
		std::vector<int> candidate;

		for (std::size_t r = 0; r < routes.size(); ++r)
		{
			bool improved = true;
			while (improved)
			{
				improved = false;
				const std::size_t m = routes[r].size();
				if (m < 3)
					break;

				const double current = s.routeDistance(routes[r]);
				for (std::size_t i = 0; i + 1 < m && !improved; ++i)
				{
					for (std::size_t j = i + 1; j < m && !improved; ++j)
					{
						candidate.assign(routes[r].begin(), routes[r].end());
						std::reverse(candidate.begin() + static_cast<std::ptrdiff_t>(i),
							candidate.begin() + static_cast<std::ptrdiff_t>(j) + 1);
						if (s.routeDistance(candidate) >= current - kEpsilon)
							continue;
						if (!s.routeFeasible(candidate))
							continue;

						routes[r] = candidate;
						improved = true;
						anyImproved = true;
					}
				}
			}
		}

		return anyImproved;
	}

	// Or-opt: relocate a run of 1-3 consecutive stops into any position of any route,
	// including its own. Emptying a route also sheds that vehicle's fixed cost, which is
	// how the fleet size shrinks.
	bool orOptMove(const Solver& s, std::vector<std::vector<int>>& routes)
	{
		// Three scratch buffers reused across every candidate move; `assign` keeps their
		// capacity, so the innermost loop does no allocation at all.
		std::vector<int> segment;
		std::vector<int> trimmed;
		std::vector<int> candidate;

		for (std::size_t a = 0; a < routes.size(); ++a)
		{
			const std::vector<int>& source = routes[a];
			const double sourceDistance = s.routeDistance(source);

			for (std::size_t pos = 0; pos < source.size(); ++pos)
			{
				for (std::size_t len = 1; len <= 3 && pos + len <= source.size(); ++len)
				{
					segment.assign(source.begin() + static_cast<std::ptrdiff_t>(pos),
						source.begin() + static_cast<std::ptrdiff_t>(pos + len));

					trimmed.assign(source.begin(), source.begin() + static_cast<std::ptrdiff_t>(pos));
					trimmed.insert(trimmed.end(), source.begin() + static_cast<std::ptrdiff_t>(pos + len), source.end());
					if (!s.routeFeasible(trimmed))
						continue;

					const double trimmedDistance = s.routeDistance(trimmed);

					for (std::size_t b = 0; b < routes.size(); ++b)
					{
						const std::vector<int>& target = (a == b) ? trimmed : routes[b];
						const double targetDistance = (a == b) ? trimmedDistance : s.routeDistance(routes[b]);

						for (std::size_t ins = 0; ins <= target.size(); ++ins)
						{
							candidate.assign(target.begin(), target.begin() + static_cast<std::ptrdiff_t>(ins));
							candidate.insert(candidate.end(), segment.begin(), segment.end());
							candidate.insert(candidate.end(),
								target.begin() + static_cast<std::ptrdiff_t>(ins), target.end());

							double delta;
							if (a == b)
							{
								delta = s.routeDistance(candidate) - sourceDistance;
							}
							else
							{
								delta = (trimmedDistance + s.routeDistance(candidate))
									- (sourceDistance + targetDistance);
								if (trimmed.empty())
									delta -= s.fixedCost;
								if (target.empty())
									delta += s.fixedCost;
							}

							if (delta >= -kEpsilon)
								continue;
							if (!s.routeFeasible(candidate))
								continue;

							if (a == b)
							{
								routes[a] = candidate;
							}
							else
							{
								routes[a] = trimmed;
								routes[b] = candidate;
							}
							return true;
						}
					}
				}
			}
		}

		return false;
	}
}

extern "C" VRP_API int Vrp_SolveCvrptw(
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
	int* outIterationCount)
{
	try
	{
		if (!costMatrix || !travelTimeMatrix || !stops || !outRouteStops || !outRouteLengths ||
			!outIterationCosts || !outIterationCount)
			return -1;
		if (stopCount <= 0 || matrixDim != stopCount + 1)
			return -1;
		if (maxVehicles <= 0 || maxIterations <= 0 || iterationCapacity <= 0)
			return -1;
		if (!(vehicleCapacity > 0.0) || !(shiftStartMinutes < shiftEndMinutes))
			return -1;
		if (routeStopsCapacity < stopCount || routeLengthsCapacity < maxVehicles)
			return -3;

		Solver solver;
		solver.cost = costMatrix;
		solver.time = travelTimeMatrix;
		solver.dim = matrixDim;
		solver.stops = stops;
		solver.stopCount = stopCount;
		solver.capacity = vehicleCapacity;
		solver.shiftStart = shiftStartMinutes;
		solver.shiftEnd = shiftEndMinutes;
		solver.fixedCost = vehicleFixedCost;

		std::vector<std::vector<int>> routes = constructSavings(solver);

		int written = 0;
		outIterationCosts[written++] = solver.objective(routes);

		for (int iteration = 0; iteration < maxIterations; ++iteration)
		{
			bool improved = twoOptPass(solver, routes);
			if (orOptMove(solver, routes))
				improved = true;

			dropEmptyRoutes(routes);

			if (written < iterationCapacity)
				outIterationCosts[written++] = solver.objective(routes);

			if (!improved)
				break;
		}

		truncateToFleet(solver, routes, maxVehicles);

		*outIterationCount = written;

		if (static_cast<int>(routes.size()) > routeLengthsCapacity)
			return -3;

		int cursor = 0;
		for (std::size_t r = 0; r < routes.size(); ++r)
		{
			if (cursor + static_cast<int>(routes[r].size()) > routeStopsCapacity)
				return -3;
			outRouteLengths[r] = static_cast<int>(routes[r].size());
			for (std::size_t k = 0; k < routes[r].size(); ++k)
				outRouteStops[cursor++] = routes[r][k];
		}

		return static_cast<int>(routes.size());
	}
	catch (...)
	{
		return -99;
	}
}
