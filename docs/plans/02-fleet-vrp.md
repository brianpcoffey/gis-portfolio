# Plan 2 — Fleet Route Optimizer (CVRPTW)

**Industry:** Logistics / last-mile delivery / field service
**Kernel:** `vrp_solver_kernel`
**Dependencies:** **Plan 0 must ship first** (needs `Graph_ComputeDistanceMatrix`)
**Target employers:** Samsara, Motive, project44, Flexport, Convoy-likes, UPS/FedEx tech, ServiceTitan, Verizon Connect

---

## The pitch

The existing Route Planner answers "what is the shortest path from A to B" — a solved
problem with a textbook answer. The problem logistics companies actually pay for is:

> I have 40 deliveries, 5 trucks, each truck holds 1,200 kg, every customer has a delivery
> window, and drivers go home at 5pm. **What should each truck do?**

That is the **Capacitated Vehicle Routing Problem with Time Windows (CVRPTW)**. It is
NP-hard, it has no exact solution at useful sizes, and every real system solves it with
metaheuristics. Building one — over the *real* Redlands road network, rendered on real
streets, with the objective visibly falling as the search improves — is the most
interview-generative project in this set.

**The visual:** press Optimize, watch five colored routes untangle themselves across the
map while a cost curve drops in real time. It is legible to a non-technical recruiter and
deep enough for a principal engineer.

---

## Glossary

| Term | Meaning |
|---|---|
| **CVRPTW** | Capacitated VRP with Time Windows. |
| **Depot** | The origin/terminus for every vehicle. |
| **Stop / customer** | A location with a demand and a time window. |
| **Demand** | Load consumed at a stop (kg, cases, pallets). |
| **Capacity** | Per-vehicle load ceiling. |
| **Time window** | `[readyTime, dueTime]` — the stop cannot be served before ready; arriving after due is infeasible. |
| **Service time** | Minutes spent at the stop, separate from travel. |
| **Route** | Depot → ordered stops → depot, assigned to one vehicle. |
| **Clarke-Wright savings** | Classic construction heuristic: `s(i,j) = d(0,i) + d(0,j) - d(i,j)`. Merge routes in descending savings order while feasible. |
| **2-opt** | Intra-route improvement: reverse a segment to remove a crossing. |
| **Or-opt** | Relocate a run of 1–3 consecutive stops elsewhere, possibly to another route. |
| **Objective** | Total travel distance, plus a fixed penalty per vehicle used. |
| **Feasible** | Satisfies capacity and every time window. |

---

## Architecture — do not repeat the Route Planner's mistake

The existing Network page downloads the 425 KB graph and **re-uploads it on every request.**
Do not copy that.

Here the client sends only the scenario — depot, stops, vehicle parameters — roughly 2 KB.
The server:

1. Snaps the depot and each stop to the nearest road node (`SnapToNearestNode`, Plan 0).
2. Builds the `(stops+1)²` road-distance matrix via `ComputeDistanceMatrixAsync` (Plan 0).
3. Converts distance → travel minutes **outside** the graph search (`km / 40 × 60`), so A\*'s
   haversine heuristic stays admissible. This matters: if minutes ever became the edge cost,
   the existing A\* would silently start returning suboptimal paths.
4. Solves the CVRPTW over the matrix.
5. Expands each route leg back into a full road-following polyline via the existing
   `FindShortestPathAsync`, so the drawn routes trace real streets.

Step 5 is what makes the demo look real rather than like a straight-line toy.

---

## Native ABI — `native/vrp_solver_kernel/include/vrp_solver_kernel.h`

```c
#pragma pack(push, 8)
struct VrpStopNative
{
	double demand;        // load consumed at this stop
	double readyTime;     // minutes from shift start
	double dueTime;       // minutes from shift start
	double serviceTime;   // minutes spent at the stop
};
#pragma pack(pop)

extern "C"
{
	// Clarke-Wright savings construction followed by 2-opt / Or-opt local search.
	//
	// costMatrix and travelTimeMatrix are row-major [matrixDim x matrixDim], where
	// index 0 is the depot and index k+1 is stops[k]. matrixDim must equal stopCount + 1.
	//
	// Routes are returned using the flat-buffer idiom: route r occupies the next
	// outRouteLengths[r] entries of outRouteStops. Values are ZERO-BASED STOP INDICES
	// (not matrix indices, not node ids) — the depot is implicit at both ends.
	//
	// outIterationCosts records the objective after each improvement pass, so the client
	// can animate convergence. outIterationCount receives the number written.
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
```

Additional status code beyond the standard set: **`-4` = no feasible solution** (a stop
whose demand exceeds capacity, or a window unreachable from the depot within the shift).
The service surfaces this as a normal result with `Feasible = false`, not an exception —
infeasibility is an answer, not an error.

---

## Algorithm

### Phase 1 — Clarke-Wright savings construction

```
route[i] = [i] for every stop i          // one route per stop
savings  = [(s(i,j), i, j)] for all i<j  // s = d(0,i) + d(0,j) - d(i,j)
sort savings descending
for each (s, i, j):
    if i is the last stop of route A and j is the first stop of route B
       and A != B
       and load(A) + load(B) <= capacity
       and merging keeps every time window feasible:
           merge B onto the end of A
```

Parallel savings (the standard variant) is enough; do not implement the λ-parameterized
version.

### Phase 2 — local search

Loop until no improving move is found or `maxIterations` is hit:

- **2-opt** within each route — reverse `route[i..j]`, accept if it lowers distance and stays
  time-feasible.
- **Or-opt** across routes — relocate a run of 1–3 consecutive stops into any position of any
  route, accept if the combined objective drops and both routes stay feasible.

Record the objective into `outIterationCosts` after each full pass.

Use **first-improvement** rather than best-improvement — it converges faster and the
difference in solution quality at this size is small. Say so on the Details page.

### Feasibility check (the part people get wrong)

Walk the route accumulating time:

```
t = shiftStart
prev = depot
for each stop s in route:
    t += travelTime[prev][s]
    t  = max(t, s.readyTime)        // wait if early — waiting is allowed
    if t > s.dueTime: INFEASIBLE    // arriving late is not
    t += s.serviceTime
    prev = s
t += travelTime[prev][depot]
if t > shiftEnd: INFEASIBLE
```

Early arrival waits; late arrival fails. Load is a simple sum against capacity.

**Objective:** `Σ route distance + vehicleFixedCost × routeCount`. The fixed cost is what
makes the solver prefer 4 trucks over 5 — without it, it will happily use every vehicle.

---

## Scenario data — `Portfolio.Services/Data/RedlandsDeliveryScenario.cs`

Deterministic LCG, same conventions as `RedlandsPropertySeedData`. Provide **three preset
scenarios** so the demo has a one-click story:

| Preset | Stops | Vehicles | Capacity | Character |
|---|---|---|---|---|
| Morning parcel run | 25 | 3 | 800 | loose windows, easy |
| Full day route | 40 | 5 | 1,200 | mixed windows |
| Tight windows | 40 | 6 | 1,000 | narrow windows, forces more vehicles |

Depot at a plausible industrial location on the west side of the network. Stops drawn from
actual road-network node coordinates (so snapping is exact and every stop is reachable),
spread across downtown and the residential grid. Demands 20–150. Windows as
`[start, start + width]` with width 60–240 minutes inside an 8-hour shift.

---

## DTOs — `Portfolio.Common/DTOs/FleetRoutingDtos.cs`

```
FleetStopDto          Id, Label, Latitude, Longitude, Demand, ReadyMinutes, DueMinutes, ServiceMinutes
FleetScenarioDto      Name, DepotLatitude, DepotLongitude, Stops, VehicleCount, VehicleCapacity,
                      ShiftStartMinutes, ShiftEndMinutes
OptimizeRequestDto    DepotLatitude, DepotLongitude, Stops, VehicleCount, VehicleCapacity,
                      ShiftStartMinutes, ShiftEndMinutes, VehicleFixedCost, MaxIterations
VehicleRouteDto       VehicleIndex, StopIds, Path (List<CoordinateDto>), DistanceKm,
                      DurationMinutes, Load, ArrivalMinutes (List<double>), Feasible
OptimizeResultDto     NativeAccelerated, Feasible, Routes, VehiclesUsed, TotalDistanceKm,
                      TotalDurationMinutes, UnservedStopIds,
                      InitialObjective, FinalObjective, ImprovementPercent,
                      IterationCosts (List<double>), MatrixBuildMs, SolveMs
```

`ArrivalMinutes` is parallel to `StopIds` — the per-stop ETA. It powers the schedule
timeline in the UI and proves the time windows are actually respected.

`MatrixBuildMs` / `SolveMs` are honest instrumentation and make good talking points; measure
with `Stopwatch` in the service.

Reuse `CoordinateDto` from `SpatialGeometryDtos.cs`.

---

## Service — `FleetRoutingService` / `IFleetRoutingService`

```csharp
Task<FleetScenarioDto> GetScenarioAsync(string presetName, CancellationToken cancellationToken = default);
Task<OptimizeResultDto> OptimizeAsync(OptimizeRequestDto request, CancellationToken cancellationToken = default);
```

Depends on `ISpatialGraphService` (constructor injection) for snapping, the matrix, and leg
expansion.

Limits:

```csharp
private const int MaxStops = 120;
private const int MaxVehicles = 20;
private const int MaxIterations = 5_000;
```

Validation messages:

| Condition | Message |
|---|---|
| no stops | `"At least one stop is required."` |
| `> MaxStops` | `$"Routing is limited to {MaxStops} stops."` |
| `vehicleCount < 1` or `> MaxVehicles` | `$"Vehicle count must be between 1 and {MaxVehicles}."` |
| `vehicleCapacity <= 0` | `"Vehicle capacity must be greater than zero."` |
| any `demand <= 0` | `"Stop demand must be greater than zero."` |
| any `demand > capacity` | `"No vehicle can serve a stop whose demand exceeds capacity."` |
| `readyMinutes > dueMinutes` | `"Stop ready time must not be later than its due time."` |
| `shiftStart >= shiftEnd` | `"Shift start must be earlier than shift end."` |
| non-finite coordinates | `"Stop coordinates must be finite values."` |
| `maxIterations` outside 1..5000 | `$"Iterations must be between 1 and {MaxIterations}."` |

The managed fallback implements the same Clarke-Wright + 2-opt/Or-opt in C#. It will be
slower — that is the point, and it is the benchmark headline for this project.

---

## API — `FleetRoutingController`, route `api/v{version:apiVersion}/fleet`

| Method | Path | Returns |
|---|---|---|
| `GET` | `/scenario?preset=fullday` | `FleetScenarioDto` |
| `POST` | `/optimize` | `OptimizeResultDto` |

`[RequestSizeLimit(4_000_000)]`, `[EnableRateLimiting("expensive")]`.

```js
fleet: {
    scenario: BASE + "/fleet/scenario",
    optimize: BASE + "/fleet/optimize"
},
```

---

## UI — `/Projects/Fleet`

Icon: `fa-solid fa-truck-fast`. Badge: "C++ VRP Solver".

**This page uses Leaflet**, matching `wwwroot/js/Network/app.js` (Leaflet 1.9, OSM tiles,
`preferCanvas: true`). Copy its map setup verbatim.

> Browser-pane note: OSM tiles will not render during automated verification — screenshots
> hang. Verify with `read_network_requests` (assert the `/fleet/optimize` 200 and inspect the
> response body) plus DOM assertions via `javascript_tool`. Do not attempt a screenshot.

**Left column — Fleet Controls**
- Preset select (3 scenarios)
- Vehicles — number input 1–10
- Capacity — number input
- Vehicle fixed cost — number input, default 25 (in km-equivalents)
- Max iterations — range slider 100–5000
- **Optimize** / **Reset**
- Per-route summary as `.geo-cell-item` rows: colored swatch, stop count, load vs capacity,
  distance, finish time. Infeasible routes use `.geo-cell-anomaly`.

**Right column**
1. **Map** — depot as a distinct marker, stops as numbered `L.circleMarker`s colored by
   assigned vehicle, routes as colored polylines following real roads. Unserved stops in
   grey with a dashed stroke.
2. **Convergence chart** — SVG line chart of `IterationCosts`. X = iteration, Y = objective.
   Annotate the initial (Clarke-Wright) and final values, and label the improvement percent.
3. **Schedule timeline** — a horizontal bar per vehicle across the shift, with each stop as a
   block positioned by `ArrivalMinutes` and sized by service time, and the time window drawn
   behind it as a lighter band. This is the view that proves time windows are respected, and
   it is what a dispatcher actually looks at.

**KPI row:** Vehicles Used · Total Distance · Longest Route · Improvement %.

---

## Tests — `Portfolio.Tests/Services/FleetRoutingServiceTests.cs`

Test the solver against a hand-built cost matrix so results are deterministic and
independent of the road network.

| Test | Assertion |
|---|---|
| `Optimize_SingleStop_ProducesOneRoute` | `Assert.False(NativeAccelerated)` first |
| `Optimize_AllStopsWithinCapacity_ServesEveryStop` | no unserved |
| `Optimize_DemandExceedsCapacity_MarksInfeasible` | `Feasible == false` |
| `Optimize_TightWindows_RespectsArrivalTimes` | every `ArrivalMinutes[i] <= dueMinutes[i]` |
| `Optimize_EarlyArrival_WaitsUntilReady` | `ArrivalMinutes[i] >= readyMinutes[i]` |
| `Optimize_HigherVehicleFixedCost_UsesFewerVehicles` | monotonicity |
| `Optimize_LoadNeverExceedsCapacity` | per-route sum |
| `Optimize_FinalObjectiveNotWorseThanInitial` | local search never regresses |
| `Optimize_IterationCosts_AreNonIncreasing` | |
| `Optimize_EveryStopAppearsExactlyOnce` | no duplicates across routes |
| `Optimize_ReturnsToDepot` | route closure implied in distance |
| `Optimize_NullRequest_ThrowsArgumentNullException` | |
| `Optimize_TooManyStops_ThrowsArgumentException` | |
| `Optimize_ShiftEndBeforeStart_ThrowsArgumentException` | |
| `Optimize_StopDemandExceedsCapacity_ThrowsArgumentException` | pre-solve validation |
| `Scenario_UnknownPreset_ThrowsArgumentException` | |
| `Scenario_IsDeterministic` | same stops across two calls |

---

## Details page — Interview Discussion Points

- CVRPTW is NP-hard. What does that actually mean for a dispatcher who needs an answer in
  30 seconds, and how do you decide when to stop searching?
- Clarke-Wright is a construction heuristic and 2-opt/Or-opt is local search. Why do you
  need both, and what happens if you skip the construction phase?
- The solver uses first-improvement rather than best-improvement. What is the tradeoff, and
  when would you switch?
- Local search gets stuck in local optima. How would you escape — simulated annealing, tabu
  search, LNS, or a genetic algorithm? What does each cost in implementation complexity?
- Distance comes from the real road network, but travel time is distance ÷ 40 km/h. What
  breaks when you introduce time-of-day traffic, and why can't you just put minutes on the
  graph edges? *(Answer: A\*'s haversine heuristic stops being admissible.)*
- The distance matrix is `(n+1)²` Dijkstras. At what stop count does matrix construction
  dominate solve time, and what would you do about it?
- How would you handle a driver going sick at 10am with half a route completed?
- What changes if stops can be served by any of three depots instead of one?
