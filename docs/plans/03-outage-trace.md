# Plan 3 — Outage Manager & Network Trace

**Industry:** Electric utilities / energy / water
**Kernel:** `network_trace_kernel`
**Dependencies:** none
**Target employers:** Esri (utilities is their largest vertical), Itron, Schneider Electric, Bentley, Oracle Utilities, GE Vernova, SCE, PG&E, Sempra

---

## The pitch

At 2am a recloser trips on a distribution feeder. The control room needs three answers in
under a minute:

1. **Who is out?** Everything downstream of the fault, stopping at open devices.
2. **What isolates it?** The nearest upstream protective device that can be opened to
   sectionalize the fault without killing the rest of the feeder.
3. **Who can we get back?** Which **tie switch** can be closed to backfeed the healthy
   sections from an adjacent feeder — and how many customers that restores.

That is an **Outage Management System**, and it is the flagship application of GIS in
electric utilities. Esri sells an entire Utility Network product around exactly this.

**Why this and not the road network:** a distribution feeder is *directed* (power flows
radially outward from the substation), its edges carry *device state* (open/closed), and it
needs *per-segment identity* to attribute customers and devices. The Redlands road graph has
none of those — every edge is bidirectional, anonymous, and attribute-free. So this gets its
own dataset and its own kernel, which is the correct answer rather than a compromise.

---

## Glossary

| Term | Meaning |
|---|---|
| **Substation** | Source. Where the feeder is energized. |
| **Feeder / circuit** | A radial tree of conductors leaving one substation breaker. |
| **Lateral** | A branch off the main trunk, usually fused. |
| **Conductor / segment** | A span of wire. The graph edge. |
| **Radial** | Tree topology — exactly one energized path from source to any point. Normal operating state. |
| **Recloser** | Automatic device that trips and re-tries a few times before locking out. |
| **Sectionalizer / switch** | Manually or remotely operated open/close point. |
| **Fuse** | One-shot protective device, typically on a lateral. |
| **Protective device** | Anything that can interrupt fault current: breaker, recloser, fuse. |
| **Tie switch** | Normally-**open** point connecting two feeders. Closing it backfeeds from the neighbor. |
| **Normally open point** | The tie switch's default state. This is what makes the network a mesh on paper and a tree in operation. |
| **Upstream trace** | Toward the source. |
| **Downstream trace** | Away from the source, toward the leaves. |
| **Isolation** | Opening devices to bound the de-energized section. |
| **Sectionalizing** | Reducing the outage to the smallest possible section. |
| **Backfeed** | Energizing a section from the opposite direction via a tie. |
| **Customers affected** | The headline number. Everything is measured against it. |
| **SAIDI** | System Average Interruption Duration Index — customer-minutes lost ÷ total customers. |
| **SAIFI** | System Average Interruption Frequency Index — interruptions ÷ total customers. |
| **CAIDI** | SAIDI ÷ SAIFI. Average outage duration per affected customer. |

SAIDI/SAIFI are the regulated performance metrics every US utility reports to its public
utilities commission. Naming them correctly is a strong credibility signal.

---

## Native ABI — `native/network_trace_kernel/include/network_trace_kernel.h`

```c
#pragma pack(push, 8)
struct TraceElementNative
{
	int id;
	int fromNodeId;      // upstream-side node under normal flow
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

	// The ordered path from the faulted element back to the source node.
	// Returns the number of ids written (>= 0), negative on error.
	TRACE_API int Trace_Upstream(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		int faultElementId,
		int* outElementIds, int outputCapacity);

	// The nearest upstream protective devices (breaker/recloser/fuse) whose opening
	// isolates the faulted element from the source, plus every switch that bounds the
	// de-energized section on the downstream side.
	// Returns the number of ids written (>= 0), negative on error.
	TRACE_API int Trace_FindIsolationDevices(
		const TraceElementNative* elements, int elementCount,
		int sourceNodeId,
		int faultElementId,
		int* outDeviceIds, int outputCapacity);

	// Connectivity sweep from the source with a set of device-state overrides applied.
	// Used to evaluate a proposed switching plan: which elements stay energized, and
	// how many customers are served.
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
```

Status codes: standard set, plus **`-5` = faulted element not found**.

### Implementation notes

- Build `std::unordered_map<int, std::vector<int>>` from node id → incident element indices.
  Use `.find()`, never `operator[]` (see the Plan 0 bug note).
- `Trace_Downstream` is a BFS from the fault element's `toNodeId`, following elements whose
  `isOpen == 0`, never revisiting an element. The fault element itself is included.
- **`Trace_ComputeEnergizedSet` must traverse undirected**, because a tie switch backfeeds
  against the normal flow direction. `fromNodeId`/`toNodeId` record the *nominal* direction
  for upstream/downstream semantics; energization is pure connectivity. This distinction is
  the single most important modelling idea in the whole project — call it out on the Details
  page.
- Overrides are applied as a small linear scan (`overrideCount` is tiny), not a rebuilt array.

---

## Dataset — `Portfolio.Services/Data/RedlandsDistributionNetwork.cs`

Deterministic, generated, `internal static class` with `Build()`, matching
`RedlandsRoadNetwork`'s shape. **Two feeders plus the tie between them** — the tie is what
makes restoration possible and is therefore non-negotiable.

```
Substation "Redlands Sub"  (node 1)
├── Feeder A  breaker A-BRK
│   ├── trunk: ~14 conductor segments
│   ├── recloser A-REC-1 at ~⅓, A-REC-2 at ~⅔
│   ├── 6 fused laterals, 3–7 segments each
│   └── transformers with 8–60 customers each
└── Feeder B  breaker B-BRK
    ├── trunk: ~12 segments
    ├── recloser B-REC-1
    ├── 5 fused laterals
    └── transformers
Tie switch T-1 (normally open) between the end of Feeder A trunk and the end of Feeder B trunk
```

Target ~420 elements, ~2,600 customers total. Give every element a human-readable label
(`"A-LAT-3-SEG-2"`, `"REC A-1"`) — the trace list is unreadable without them.

Lay it out with real-ish coordinates over Redlands so it renders as a geographic tree, using
a recursive branch generator with angle jitter from the LCG.

---

## DTOs — `Portfolio.Common/DTOs/OutageDtos.cs`

```
NetworkElementDto     Id, Label, FromNodeId, ToNodeId, DeviceType, IsOpen, CustomerCount,
                      FromLatitude, FromLongitude, ToLatitude, ToLongitude, FeederName
DistributionNetworkDto NetworkName, SourceNodeId, Elements, TotalCustomers, FeederNames

TraceRequestDto       Elements, SourceNodeId, FaultElementId
TraceResultDto        NativeAccelerated, FaultElementId,
                      DownstreamElementIds, UpstreamElementIds, IsolationDeviceIds,
                      CustomersAffected, CustomersTotal, PercentAffected

RestoreRequestDto     Elements, SourceNodeId, FaultElementId, IsolationDeviceIds
SwitchingStepDto      ElementId, Label, Action           // "OPEN" | "CLOSE"
RestoreResultDto      NativeAccelerated, Plan (List<SwitchingStepDto>),
                      CustomersRestored, CustomersStillOut, EnergizedElementIds,
                      EstimatedSaidiMinutesAvoided, RestorationFound
```

`DeviceType` crosses the wire as `int` to match the native struct. Provide a
`DeviceTypes` static class of named constants in `Portfolio.Common` rather than a magic
number in the JS — and mirror the names in the frontend legend.

---

## Service — `OutageTraceService` / `IOutageTraceService`

```csharp
Task<DistributionNetworkDto> GetNetworkAsync(CancellationToken cancellationToken = default);
Task<TraceResultDto> TraceAsync(TraceRequestDto request, CancellationToken cancellationToken = default);
Task<RestoreResultDto> ProposeRestorationAsync(RestoreRequestDto request, CancellationToken cancellationToken = default);
```

### Restoration search (managed orchestration over the native connectivity sweep)

```
1. Apply the isolation devices as OPEN overrides.
2. Baseline = Trace_ComputeEnergizedSet with just those overrides → customersServed_base.
3. For each tie switch currently open:
       candidate = baseline overrides + { tie: CLOSED }
       served    = Trace_ComputeEnergizedSet(candidate)
       if served > best: record it
4. Emit the winning plan as ordered SwitchingStepDto:
       OPEN each isolation device, then CLOSE the winning tie.
5. CustomersRestored = best - customersServed_base
```

The loop is managed, each evaluation is native. That is the right boundary and worth
defending: the search is small and readable, the sweep is the hot inner work.

`EstimatedSaidiMinutesAvoided` = `customersRestored × assumedRepairMinutes / totalCustomers`,
with `assumedRepairMinutes` a request parameter defaulting to 180. Label it clearly as an
estimate in the UI.

Limits: `MaxElements = 5_000`, `MaxOverrides = 100`.

Validation messages:

| Condition | Message |
|---|---|
| no elements | `"At least one network element is required."` |
| `> MaxElements` | `$"Networks are limited to {MaxElements} elements."` |
| fault id not present | `"The faulted element was not found in the network."` |
| source node not referenced by any element | `"The source node was not found in the network."` |
| `customerCount < 0` | `"Customer count cannot be negative."` |
| unknown `deviceType` | `"Device type must be between 0 and 6."` |

---

## API — `OutageController`, route `api/v{version:apiVersion}/outage`

| Method | Path | Returns |
|---|---|---|
| `GET` | `/network` | `DistributionNetworkDto` |
| `POST` | `/trace` | `TraceResultDto` |
| `POST` | `/restore` | `RestoreResultDto` |

```js
outage: {
    network: BASE + "/outage/network",
    trace  : BASE + "/outage/trace",
    restore: BASE + "/outage/restore"
},
```

---

## UI — `/Projects/Outage`

Icon: `fa-solid fa-bolt`. Badge: "C++ Trace Kernel".

**Self-contained SVG** — no map tiles. The network is a tree with real coordinates; render
it directly. This keeps the page verifiable in the Browser pane.

**Left column — Outage Controls**
- Feeder filter (All / Feeder A / Feeder B)
- Assumed repair time (minutes), default 180
- **Propose Restoration** button — disabled until a fault is placed
- **Clear Fault**
- Device legend: colored swatch per `deviceType`, plus open vs closed styling
- Switching plan as an ordered `<ol>` of `.geo-cell-item` rows once restoration runs
- `<pre class="sample-json">` echo

**Right column — Single-line diagram**
- Every element drawn as an SVG `<line>` between its endpoint coordinates.
- Devices drawn as shapes at the midpoint: switch = square, fuse = small rectangle,
  recloser = circle, breaker = double square, tie switch = open square with dashed stroke,
  transformer = triangle.
- **Click any conductor to place a fault.** On click, call `/trace` and repaint:
  - downstream (de-energized) → red, thicker stroke
  - upstream path to source → blue
  - isolation devices → amber halo
  - everything else → `var(--text-muted)` at low opacity
- After restoration, restored sections turn green and the closed tie animates its stroke.

**KPI row:** Customers Affected · % of System · Isolation Devices · Customers Restored.

Interaction detail worth building: hovering an element shows a small tooltip with its label,
device type, and customer count. Utility software lives on that.

---

## Tests — `Portfolio.Tests/Services/OutageTraceServiceTests.cs`

Build small hand-authored networks — a 6-element radial with one lateral is enough for most
cases, plus a 2-feeder network with a tie for restoration.

| Test | Assertion |
|---|---|
| `Trace_FaultOnLateral_AffectsOnlyThatLateral` | `Assert.False(NativeAccelerated)` first |
| `Trace_FaultOnTrunk_AffectsEverythingDownstream` | |
| `Trace_StopsAtOpenDevice` | element beyond an open switch not included |
| `Trace_CustomersAffected_SumsDownstreamCustomers` | hand-computed |
| `Trace_UpstreamPath_ReachesSource` | last element is the breaker |
| `Trace_IsolationDevices_ReturnsNearestUpstreamProtective` | fuse before recloser before breaker |
| `Trace_FaultElementItselfIsIncludedDownstream` | |
| `Trace_UnknownFaultElement_ThrowsArgumentException` | |
| `Trace_NullRequest_ThrowsArgumentNullException` | |
| `Trace_TooManyElements_ThrowsArgumentException` | |
| `Restore_ClosingTie_RestoresHealthySections` | `CustomersRestored > 0` |
| `Restore_NoTieAvailable_ReturnsNoRestoration` | `RestorationFound == false` |
| `Restore_PlanOpensIsolationBeforeClosingTie` | step order |
| `Restore_DoesNotReenergizeFaultedSection` | fault element not in `EnergizedElementIds` |
| `Restore_CustomersRestoredPlusStillOut_EqualsAffected` | conservation |
| `EnergizedSet_TraversesTieAgainstNominalDirection` | the backfeed case |
| `Network_IsDeterministic` | `Assert.Same` on the materialized element list |
| `Network_IsRadial_WithTiesOpen` | exactly one path from source to every element |

That last one is a genuinely valuable invariant test — it asserts the generated network is
electrically valid.

---

## Details page — Interview Discussion Points

- The network is stored with directed `from`/`to` but energization is computed
  undirected. Why, and what would break if you traced connectivity directionally?
- A distribution feeder is radial in operation but meshed on paper. How does that change
  the data model versus a road network?
- What is the difference between isolating a fault and restoring customers, and why do they
  need separate algorithms?
- Closing a tie switch backfeeds from an adjacent feeder. What real-world constraints would
  stop you — conductor ampacity, voltage drop, protection coordination — and how would you
  model them?
- SAIDI is customer-minutes ÷ total customers. Why do utilities optimize for it, and what
  behavior does that incentivize that might not serve customers well?
- The restoration search evaluates every tie one at a time. When does that stop working, and
  what would you do for a network with 200 ties?
- How would you extend this to handle a partial trace when the SCADA state of a device is
  unknown?
