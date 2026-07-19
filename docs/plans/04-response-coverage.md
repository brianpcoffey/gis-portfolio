# Plan 4 — Emergency Response Coverage Optimizer

**Industry:** Public safety / GovTech
**Kernel:** `facility_location_kernel`
**Dependencies:** **Plan 0 must ship first** (needs `Graph_ComputeDistances` and `Graph_ComputeDistanceMatrix`)
**Target employers:** Tyler Technologies, Axon, Motorola Solutions, CentralSquare, Hexagon Safety, RapidDeploy, Esri public-safety

---

## The pitch

A fire chief has a fixed budget and a political question: **where do the stations go?**

The Hotspot Clusterer already finds where incidents concentrate. This closes the loop —
it answers what to *do* about it:

1. **Isochrones** — from a station, which streets are reachable within 4 minutes? 8? 12?
   Not a circle on a map, but actual drive-time bands over the real road network.
2. **Coverage optimization** — given candidate sites and demand weighted by historical call
   volume, pick K stations that minimize the **90th-percentile response time**.

**NFPA 1710** is the standard: a first-due engine on scene within **4 minutes travel time**
for 90% of incidents, and an ALS unit within **8 minutes**. Every US fire department is
measured against it. Building the tool that evaluates compliance is squarely in the domain.

**Why p90 and not the mean:** the standard is written as a percentile precisely because a
mean hides the neighborhood that always waits 11 minutes. Optimizing the right objective is
the interesting part of this project, and the UI should let you toggle between mean and p90
and *watch the chosen stations move*. That comparison is the demo.

---

## Glossary

| Term | Meaning |
|---|---|
| **Isochrone** | The set of locations reachable within a given travel time. |
| **Service area** | Same idea, often used for the polygon form. |
| **Demand point** | A location generating calls, weighted by historical volume. |
| **Candidate site** | A location where a station *could* go. |
| **Facility** | A chosen station. |
| **p-median problem** | Choose p facilities minimizing total weighted distance to demand. |
| **MCLP** | Maximum Covering Location Problem — maximize demand covered within a threshold. |
| **Teitz-Bart** | Vertex-substitution heuristic for p-median: repeatedly swap one open facility for a closed candidate while it improves. |
| **Response time** | Turnout + travel. This model uses travel only; say so explicitly. |
| **First-due** | The unit whose assigned area covers the incident. |
| **NFPA 1710** | Career-department standard: 4-min travel for first engine, 8-min for ALS, 90% of the time. |
| **CAD** | Computer-Aided Dispatch — the system these numbers come from. |

---

## Native ABI — `native/facility_location_kernel/include/facility_location_kernel.h`

```c
extern "C"
{
	// p-median / p-center via greedy seeding + Teitz-Bart vertex substitution.
	//
	// costMatrix is row-major [candidateCount x demandCount], travel time in minutes.
	// cost[c * demandCount + d] is the travel time from candidate c to demand d.
	//
	// objectiveMode: 0 = weighted mean response time
	//                1 = weighted 90th-percentile response time
	//                2 = fraction of demand NOT covered within coverageThreshold (MCLP)
	//
	// outIterationObjectives records the objective after each substitution pass so the
	// client can animate convergence.
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

	// Evaluate a fixed configuration: nearest-facility assignment and the response-time
	// distribution. outAssignment and outResponseTimes are parallel to the demand array.
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
```

### Implementation notes

- **The hot loop is the substitution evaluation.** Teitz-Bart tries every (open facility,
  closed candidate) pair; each trial requires recomputing the nearest-facility assignment
  over all demand points. Naively that is
  `O(p × (candidates - p) × demand × p)` per pass — genuinely expensive, and the honest
  justification for native code.
- **Optimize it the standard way and say so:** maintain per-demand-point *nearest* and
  *second-nearest* facility distances. Removing a facility only requires re-scanning demand
  points whose nearest was the removed one; adding a candidate is a single pass comparing
  against the current nearest. This turns each trial from `O(demand × p)` into `O(demand)`.
  Explaining that optimization is excellent interview material.
- **Weighted p90:** sort demand points by response time, accumulate weight, and take the
  value where cumulative weight first crosses 90% of total weight. It is *not* the p90 of the
  unweighted list — a common and revealing mistake.
- **Greedy seed:** pick the candidate minimizing the objective alone, then repeatedly add the
  candidate that most improves it, until `facilityCount` are chosen. Then run substitution.

---

## Isochrones

`Graph_ComputeDistances` (Plan 0) returns the cost to every node from an origin. The service
converts km → minutes (`km / avgSpeedKmh × 60`) and buckets nodes into bands.

Expose average speed as a request parameter (default 40 km/h, range 20–80) — a fire
apparatus running code is not a delivery van, and letting the user change it makes the model
feel real rather than hardcoded.

**Bands: 0–4, 4–8, 8–12, >12 minutes**, matching NFPA thresholds. Return band index per
reachable node so the client can color them.

---

## Data — `Portfolio.Services/Data/RedlandsResponseScenario.cs`

Deterministic LCG. Three collections:

- **~450 demand points** — road-network node coordinates weighted by call volume. Generate
  clustered volume (downtown core high, residential moderate, industrial low) rather than
  uniform, so optimization has something meaningful to find. Reuse the clustering idea from
  the Hotspot Clusterer's generator.
- **~24 candidate sites** — plausible station locations on the network, including the 2
  existing stations so the UI can show "current vs optimized".
- **2 existing stations** — so the headline comparison is *improvement over today*.

Every location must snap exactly to a road-network node so the matrix has no unreachable
pairs.

---

## DTOs — `Portfolio.Common/DTOs/ResponseCoverageDtos.cs`

```
DemandPointDto        Id, NodeId, Latitude, Longitude, CallVolume
CandidateSiteDto      Id, NodeId, Label, Latitude, Longitude, IsExisting
ResponseScenarioDto   Name, DemandPoints, Candidates, ExistingStationIds, TotalCallVolume

IsochroneRequestDto   OriginNodeId, AvgSpeedKmh, BandMinutes (List<double>)
IsochroneNodeDto      NodeId, Latitude, Longitude, Minutes, BandIndex
IsochroneResultDto    NativeAccelerated, OriginNodeId, Nodes, BandCounts,
                      ReachableNodes, UnreachableNodes

OptimizeCoverageRequestDto   DemandPoints, Candidates, StationCount, ObjectiveMode,
                             AvgSpeedKmh, FirstThresholdMinutes, SecondThresholdMinutes,
                             MaxIterations
CoverageStatsDto      MeanMinutes, P50Minutes, P90Minutes,
                      PercentWithinFirstThreshold, PercentWithinSecondThreshold
DemandAssignmentDto   DemandPointId, AssignedCandidateId, ResponseMinutes
OptimizeCoverageResultDto  NativeAccelerated, ChosenCandidateIds, Optimized (CoverageStatsDto),
                           Baseline (CoverageStatsDto), Assignments,
                           IterationObjectives (List<double>),
                           MatrixBuildMs, SolveMs, MeetsNfpa1710
```

`Baseline` is the same stats computed for the existing stations. **That side-by-side is the
whole point of the page** — "current p90 is 7.8 minutes, optimized is 5.1" is the sentence
a chief repeats to a city council.

`MeetsNfpa1710` is `PercentWithinFirstThreshold >= 90`.

---

## Service — `ResponseCoverageService` / `IResponseCoverageService`

```csharp
Task<ResponseScenarioDto> GetScenarioAsync(CancellationToken cancellationToken = default);
Task<IsochroneResultDto> ComputeIsochroneAsync(IsochroneRequestDto request, CancellationToken cancellationToken = default);
Task<OptimizeCoverageResultDto> OptimizeAsync(OptimizeCoverageRequestDto request, CancellationToken cancellationToken = default);
```

Injects `ISpatialGraphService`.

Limits:

```csharp
private const int MaxDemandPoints = 2_000;
private const int MaxCandidates = 200;
private const int MaxStations = 20;
private const int MaxIterations = 500;
```

Validation messages:

| Condition | Message |
|---|---|
| no demand points | `"At least one demand point is required."` |
| `> MaxDemandPoints` | `$"Coverage analysis is limited to {MaxDemandPoints} demand points."` |
| no candidates | `"At least one candidate site is required."` |
| `> MaxCandidates` | `$"Coverage analysis is limited to {MaxCandidates} candidate sites."` |
| `stationCount < 1` | `"Station count must be at least one."` |
| `stationCount > candidates.Count` | `"Station count cannot exceed the number of candidate sites."` |
| `stationCount > MaxStations` | `$"Station count is limited to {MaxStations}."` |
| `callVolume <= 0` | `"Call volume must be greater than zero."` |
| `avgSpeedKmh` outside 5–120 | `"Average speed must be between 5 and 120 km/h."` |
| `objectiveMode` not 0–2 | `"Objective mode must be 0 (mean), 1 (p90), or 2 (coverage)."` |
| empty or unsorted band list | `"Isochrone bands must be ascending positive values."` |
| unknown origin node | `"The origin node was not found in the road network."` |

---

## API — `ResponseCoverageController`, route `api/v{version:apiVersion}/response`

| Method | Path | Returns |
|---|---|---|
| `GET` | `/scenario` | `ResponseScenarioDto` |
| `POST` | `/isochrone` | `IsochroneResultDto` |
| `POST` | `/optimize` | `OptimizeCoverageResultDto` |

`[RequestSizeLimit(4_000_000)]`, `[EnableRateLimiting("expensive")]`.

```js
response: {
    scenario : BASE + "/response/scenario",
    isochrone: BASE + "/response/isochrone",
    optimize : BASE + "/response/optimize"
},
```

---

## UI — `/Projects/Response`

Icon: `fa-solid fa-truck-medical`. Badge: "C++ Facility Location".

**Leaflet page**, same setup as `wwwroot/js/Network/app.js`. Same Browser-pane caveat as
Plan 2 — verify with `read_network_requests` and DOM assertions, never screenshots.

**Left column — Coverage Controls**
- Station count — range slider 1–8, live readout
- Objective — radio: *Mean response* / *90th percentile* / *Max coverage*
- Average speed (km/h) — number input, default 40
- NFPA thresholds — two number inputs, defaults 4 and 8
- **Optimize Stations** / **Show Isochrone** / **Reset to Existing**
- Chosen-station list as `.geo-cell-item` rows with label + assigned demand + p90
- `<pre class="sample-json">` echo

**Right column**
1. **Map** — demand points as circles sized by call volume and colored by response time
   (green → amber → red on a 0–12 min ramp). Existing stations as grey markers, optimized
   stations as accent markers, unselected candidates as small hollow circles. Draw a faint
   line from each demand point to its assigned station — the assignment "starburst" makes
   the districting instantly legible.
2. **Isochrone overlay** — clicking any station paints reachable road nodes by band
   (4/8/12 min) as colored dots. Toggleable.
3. **Response-time histogram** — SVG bar chart, x = minutes in 1-min bins, y = weighted call
   volume, with vertical marker lines at the two NFPA thresholds and a shaded region past
   p90. Overlay the baseline distribution as a translucent outline so the improvement is
   visible in one glance.

**KPI row:** p90 Response · % within 4 min · % within 8 min · NFPA 1710 (Pass/Fail badge).

The Pass/Fail badge should be green/red and large. It is the number the whole page exists
to produce.

---

## Tests — `Portfolio.Tests/Services/ResponseCoverageServiceTests.cs`

Use hand-built cost matrices so results are deterministic and independent of the road graph.

| Test | Assertion |
|---|---|
| `Optimize_SingleCandidate_ChoosesIt` | `Assert.False(NativeAccelerated)` first |
| `Optimize_TwoClusters_PlacesOneStationPerCluster` | the core sanity case |
| `Optimize_MoreStations_NeverWorsensObjective` | monotonicity in p |
| `Optimize_P90Objective_DiffersFromMeanObjective` | on a deliberately skewed matrix |
| `Optimize_WeightedP90_RespectsCallVolume` | heavy-weight point dominates |
| `Optimize_IterationObjectives_AreNonIncreasing` | |
| `Optimize_AssignmentIsNearestChosenFacility` | every demand point |
| `Optimize_MeetsNfpa_TrueWhenNinetyPercentWithinFirstThreshold` | boundary at exactly 90 |
| `Optimize_CoverageMode_MaximizesDemandWithinThreshold` | |
| `Optimize_StationCountExceedsCandidates_ThrowsArgumentException` | |
| `Optimize_NullRequest_ThrowsArgumentNullException` | |
| `Optimize_ZeroCallVolume_ThrowsArgumentException` | |
| `Isochrone_BandsPartitionReachableNodes` | every reachable node in exactly one band |
| `Isochrone_OriginIsInFirstBand` | zero minutes |
| `Isochrone_UnreachableNodesExcluded` | infinite distance |
| `Isochrone_HigherSpeed_ExpandsBands` | monotonicity |
| `Isochrone_UnsortedBands_ThrowsArgumentException` | |
| `Isochrone_UnknownOrigin_ThrowsArgumentException` | |
| `Scenario_IsDeterministic` | |
| `Scenario_EveryLocationSnapsToRoadNode` | no unreachable pairs |

---

## Details page — Interview Discussion Points

- NFPA 1710 is written as a 90th percentile, not a mean. Why does that distinction change
  where you put stations, and what does optimizing the mean hide?
- p-median is NP-hard. Teitz-Bart is a local-search heuristic — how far from optimal can it
  be, and how would you bound that?
- Weighted p90 is not the p90 of the unweighted list. Why does that matter here?
- The substitution loop caches nearest and second-nearest distances per demand point.
  Explain the speedup and what breaks if you only cache the nearest.
- Response time here is travel only — no turnout, no dispatch delay, no traffic. Which of
  those most distorts the answer, and how would you get real numbers?
- Isochrones are computed as node sets, not polygons. When would you need real polygons,
  and how would you build them from the node set?
- This model assumes the nearest station always responds. What actually happens when it is
  already on a call, and how would you model unit availability?
- How would you handle the political constraint that you cannot close an existing station?
