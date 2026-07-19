# Plan 1 — Catastrophe Risk Analyzer

**Industry:** Insurance / reinsurance / InsurTech
**Kernel:** `cat_risk_kernel`
**Dependencies:** none — build this first
**Target employers:** Verisk, CoreLogic, Guidewire, Duck Creek, Milliman, Swiss Re, Munich Re, State Farm, Mercury, Farmers

---

## The pitch

A carrier holds a **book** of policies — thousands of insured locations, each with a
**Total Insured Value (TIV)**. Three questions decide whether they stay solvent:

1. **Where is my hazard?** Per-location wildfire exposure from terrain and fuel proximity.
2. **Where am I concentrated?** How much TIV sits inside any single catastrophe footprint —
   because one event hitting one canyon can wipe out a year of premium.
3. **What could it cost me?** The **exceedance probability curve** — the loss at the 1-in-100
   and 1-in-250 year return periods, and the **average annual loss** that has to be priced in.

This is what a catastrophe model does, and it is the single most valuable analytical product
in property insurance. Almost no portfolio outside the industry contains one.

**Why it fits this repo:** the terrain kernel already computes slope and aspect, the overlay
kernel already does point-in-polygon, the clustering kernel already finds density. The
missing piece — Monte Carlo event simulation — is embarrassingly parallel dense numeric
work over a flat array, which is the most defensible native-code justification in the
entire portfolio: **50 million site-event evaluations per run.**

---

## Glossary — use these words, spell them correctly

| Term | Meaning |
|---|---|
| **TIV** | Total Insured Value. The replacement cost of the insured property. |
| **Exposure** | The set of insured locations and their values. "The book." |
| **Accumulation** | Aggregated TIV within a geographic footprint. The core concentration control. |
| **Ring analysis** | Accumulation within radius R of each location. The classic method. |
| **Vulnerability curve** | Maps hazard intensity → **mean damage ratio (MDR)**, the fraction of TIV lost. |
| **MDR** | Mean Damage Ratio, 0–1. |
| **Ground-up loss** | Loss before deductible and limit are applied. |
| **Gross loss** | Loss after deductible and limit. What the carrier actually pays. |
| **Deductible** | Retained by the insured. Applied per-location here. |
| **Limit** | Maximum payout per location. Caps the loss. |
| **AAL** | Average Annual Loss = Σ(event rate × event loss). The expected annual cost; feeds pricing. |
| **EP curve** | Exceedance Probability curve. Loss vs. probability of being exceeded in a year. |
| **OEP** | Occurrence EP — the largest *single event* loss at a given return period. |
| **AEP** | Aggregate EP — total *annual* loss at a given return period. |
| **Return period** | 1 / annual exceedance probability. "1-in-250" = 0.4% annual chance. |
| **PML** | Probable Maximum Loss. Conventionally the OEP at 1-in-250. |
| **WUI** | Wildland–Urban Interface. Where structures meet wildland fuel. The wildfire risk zone. |
| **Event set / catalog** | The stochastic set of simulated events with annual frequencies. |

Do **not** write "risk score" where the industry says "MDR", and do not conflate OEP with AEP.

---

## Native ABI — `native/cat_risk_kernel/include/cat_risk_kernel.h`

```c
#pragma pack(push, 8)
struct CatLocationNative
{
	double latitude;
	double longitude;
	double insuredValue;     // TIV, dollars
	double siteHazard;       // 0..1 baseline susceptibility (slope + fuel + WUI)
	double deductibleRate;   // fraction of TIV retained by the insured
	double limitRate;        // fraction of TIV that caps the payout
};

struct CatEventNative
{
	double latitude;         // epicenter
	double longitude;
	double intensity;        // 0..1 event severity at the epicenter
	double radiusKm;         // footprint radius; intensity decays linearly to 0 at the edge
	double annualRate;       // Poisson frequency, events per year
};
#pragma pack(pop)

extern "C"
{
	// Ring accumulation. For each location, sums the TIV of every location within
	// radiusKm (including itself). Brute-force O(n^2) haversine — this is the
	// workload the native path exists to accelerate.
	// outRingTiv is parallel to `locations`. Returns 0 on success, negative on error.
	CAT_API int Cat_ComputeRingAccumulation(
		const CatLocationNative* locations,
		int locationCount,
		double radiusKm,
		double* outRingTiv,
		int outputLength);

	// Monte Carlo event loss simulation.
	// For each event e and location l:
	//   d          = haversine(event, location)
	//   siteInt    = event.intensity * max(0, 1 - d / event.radiusKm) * location.siteHazard
	//   mdr        = 1 - exp(-vulnerabilityAlpha * siteInt)          // bounded [0, 1)
	//   groundUp   = location.insuredValue * mdr
	//   gross      = clamp(groundUp - TIV*deductibleRate, 0, TIV*limitRate)
	// outEventLosses[e] is the summed gross loss across all locations for event e.
	// Returns 0 on success, negative on error.
	CAT_API int Cat_SimulateEventLosses(
		const CatLocationNative* locations,
		int locationCount,
		const CatEventNative* events,
		int eventCount,
		double vulnerabilityAlpha,
		double* outEventLosses,
		int outputLength);
}
```

**Early-out worth implementing and mentioning:** skip the whole inner body when
`d > event.radiusKm`. A cheap bounding-box reject before the haversine cuts most of the
work on a spatially clustered book. Say so in the Details page — it is the difference
between a naive loop and one that understands its data.

`vulnerabilityAlpha` is the curve shape; `3.0` is a reasonable default (site intensity 0.5
→ MDR ≈ 0.78). Expose it in the UI as "Vulnerability" so the shape is explorable.

---

## Managed derivation (do NOT put this in native code)

The kernel returns per-event losses. The service turns them into the actual deliverables.
This split is deliberate and worth defending in an interview: dense parallel arithmetic goes
native, statistical shaping stays managed where it is readable and testable.

**AAL** — `Σ(event.annualRate × eventLoss[e])`.

**OEP curve** — for a loss level `L`, the annual exceedance rate is
`λ(L) = Σ{e : loss[e] > L} annualRate[e]`, and the return period is `1 / λ(L)`.

To produce the curve: sort events by loss descending, accumulate `annualRate`, and emit a
`(returnPeriod = 1 / cumulativeRate, loss)` point per event. Then interpolate the loss at
each requested return period.

Report at **10, 25, 50, 100, 250, 500** years. **PML = OEP at 250.**

**Concentration breach** — flag any location whose ring TIV exceeds a configurable limit.
Report the count, the worst offender, and its ring TIV.

> **Calibrated during implementation:** defaults are **3 km / $200M**, not the 5 km / $50M
> originally planned. At 5 km an entire community fits inside one ring, so the top decile
> saturates at a single value and *every* location breaches a $50M limit — the KPI reads
> 900/900 and the map outlines everything. At 3 km the ring distribution spreads properly
> (p50 $88M, p90 $194M, max $292M) and a $200M limit flags roughly the top 10%. Pick demo
> thresholds from the measured distribution, not from a round number.

---

## Dataset — `Portfolio.Services/Data/SoCalPolicyBook.cs`

**Generated in code, not persisted.** Follow `RedlandsRoadNetwork` (an `internal static
class` in `Portfolio.Services.Data` with a `Build()` method), not the EF seeding path — this
is a stateless MVP like every other spatial-compute project, and a 900-row `InsertData`
migration buys nothing here.

Use the deterministic LCG from `RedlandsPropertySeedData` (Numerical Recipes constants,
fixed seed, no `Random`, no `DateTime.Now`) so the book is byte-identical every run.

**~900 policy locations** across the San Bernardino / Riverside foothills — genuine WUI
territory, and it keeps the geography adjacent to the rest of the portfolio without being a
seventh Redlands dataset. Structure as 6 named communities with different risk profiles:

| Community | Character | Site hazard | TIV range |
|---|---|---|---|
| Forest Falls | canyon, heavy fuel, steep | 0.75–0.95 | $400k–1.2M |
| Oak Glen | foothill orchard, moderate fuel | 0.55–0.75 | $500k–1.5M |
| Yucaipa Ridge | ridgeline WUI | 0.60–0.85 | $450k–1.1M |
| Redlands Heights | urban edge, irrigated | 0.30–0.50 | $600k–2.5M |
| San Bernardino Flats | urban core, low fuel | 0.10–0.25 | $250k–700k |
| Cherry Valley | rural grass, wind-exposed | 0.45–0.70 | $300k–900k |

Correlate fields the way `RedlandsPropertySeedData` does — derive `siteHazard` from a
slope proxy and a distance-to-fuel proxy rather than drawing it independently, so the map
reads as terrain-driven rather than random noise. Deductible 2–5% of TIV (wildfire
deductibles are percentage-based, not flat — this detail signals domain knowledge). Limit
rate 0.8–1.0.

**Event catalog** — generate ~5,000 stochastic wildfire events, also deterministically:
epicenters biased toward high-fuel terrain, `radiusKm` 2–25 lognormal-ish, `intensity`
0.3–1.0, `annualRate` inversely related to severity (big fires are rare). Total catalog rate
should land near 1.5 events/year for the region.

Expose `SoCalPolicyBook.Locations` and `SoCalPolicyBook.EventCatalog` as
`IReadOnlyList<T>` materialized once via a static property initializer, matching
`RedlandsPropertySeedData.All`.

---

## DTOs — `Portfolio.Common/DTOs/CatRiskDtos.cs`

```
CatLocationDto        Id, Name, Community, Latitude, Longitude, InsuredValue,
                      SiteHazard, DeductibleRate, LimitRate
CatEventDto           Id, Latitude, Longitude, Intensity, RadiusKm, AnnualRate
PolicyBookDto         BookName, Locations (List<CatLocationDto>), TotalInsuredValue, LocationCount

AccumulationRequestDto   Locations, RadiusKm, ConcentrationLimit
AccumulationResultDto    NativeAccelerated, RadiusKm, ConcentrationLimit,
                         Rings (List<RingDto>), BreachCount, WorstRingTiv, WorstLocationId
RingDto                  LocationId, RingTiv, NeighborCount, Breached

SimulationRequestDto     Locations, Events, VulnerabilityAlpha
SimulationResultDto      NativeAccelerated, EventCount, LocationCount,
                         AverageAnnualLoss, ProbableMaximumLoss,
                         ExceedanceCurve (List<ExceedancePointDto>),
                         ReturnPeriodLosses (List<ReturnPeriodLossDto>),
                         WorstEvent (EventLossDto)
ExceedancePointDto       ReturnPeriod, Loss
ReturnPeriodLossDto      ReturnPeriod, Loss
EventLossDto             EventId, Latitude, Longitude, Loss, AffectedLocations
```

Plain classes, `{ get; set; }`, `= []`, XML doc on every member, `NativeAccelerated` on both
result DTOs.

---

## Service — `CatRiskService` / `ICatRiskService`

```csharp
Task<PolicyBookDto> GetPolicyBookAsync(CancellationToken cancellationToken = default);
Task<AccumulationResultDto> ComputeAccumulationAsync(AccumulationRequestDto request, CancellationToken cancellationToken = default);
Task<SimulationResultDto> SimulateAsync(SimulationRequestDto request, CancellationToken cancellationToken = default);
```

Limits:

```csharp
private const int MaxLocations = 5_000;
private const int MaxEvents = 20_000;
private const long MaxEvaluations = 60_000_000;   // locations * events
private const double MaxRadiusKm = 200.0;
```

Validation messages (these are the public API contract):

| Condition | Message |
|---|---|
| no locations | `"At least one policy location is required."` |
| `> MaxLocations` | `$"Analyses are limited to {MaxLocations} policy locations."` |
| no events | `"At least one catastrophe event is required."` |
| `> MaxEvents` | `$"Event catalogs are limited to {MaxEvents} events."` |
| `locations * events > MaxEvaluations` | `$"Simulation size is limited to {MaxEvaluations} site-event evaluations."` |
| non-finite lat/lon | `"Location coordinates must be finite values."` |
| `insuredValue <= 0` | `"Insured value must be greater than zero."` |
| `siteHazard` outside 0–1 | `"Site hazard must be between 0 and 1."` |
| `deductibleRate` outside 0–1 | `"Deductible rate must be between 0 and 1."` |
| `limitRate` outside 0–1 | `"Limit rate must be between 0 and 1."` |
| `radiusKm <= 0` or `> MaxRadiusKm` | `$"Ring radius must be between 0 and {MaxRadiusKm} km."` |
| `vulnerabilityAlpha <= 0` | `"Vulnerability alpha must be greater than zero."` |
| non-finite event radius/intensity/rate | `"Event parameters must be finite values."` |

The managed fallback must reproduce the native arithmetic **exactly** — same haversine, same
decay, same clamp order. Parity is the whole point of the pattern. Write the loss formula
once as a `private static double GrossLoss(...)` used by the managed path, and mirror it
line-for-line in the C++.

---

## API — `CatRiskController`, route `api/v{version:apiVersion}/catrisk`

| Method | Path | Body | Returns |
|---|---|---|---|
| `GET` | `/book` | — | `PolicyBookDto` |
| `POST` | `/accumulation` | `AccumulationRequestDto` | `AccumulationResultDto` |
| `POST` | `/simulate` | `SimulationRequestDto` | `SimulationResultDto` |

`[RequestSizeLimit(8_000_000)]` on both POSTs — a 5,000-location book plus a 20,000-event
catalog is a large payload. Add `[EnableRateLimiting("expensive")]` to the class.

`GET /book` should also carry `[ResponseCache(Duration = 3600)]` — it is a compile-time
constant.

Route config in `api-config.js`:

```js
catRisk: {
    book        : BASE + "/catrisk/book",
    accumulation: BASE + "/catrisk/accumulation",
    simulate    : BASE + "/catrisk/simulate"
},
```

---

## UI — `/Projects/CatRisk`

Icon: `fa-solid fa-fire-flame-curved`. Badge: "C++ CAT Kernel".

**Left column (`col-lg-4`) — Analysis Controls**
- Ring radius (km) — number input, 1–50, default 5
- Concentration limit ($M) — number input, default 50
- Vulnerability alpha — range slider 0.5–8, default 3, with live value readout
- Event catalog size — select: 1,000 / 5,000 / 20,000
- **Run Analysis** (`btn-accent`) and **Reload Book** (`btn-outline-accent`)
- Per-community TIV breakdown as `.geo-cell-item` rows
- `<pre class="sample-json">` echoing AAL / PML / native flag

**Right column (`col-lg-8`) — two stacked panels**

1. **Exposure map** — self-contained SVG, no tiles. Policy locations as circles: radius
   scaled by TIV, fill interpolated on `siteHazard` (a green→amber→red ramp). Ring-breach
   locations get a pulsing stroke. The worst event's footprint draws as a translucent circle
   overlay after a simulation. Communities labelled.

2. **EP curve** — SVG line chart, **log-scale X axis** for return period (10 → 500), linear
   Y for loss in $M. Plot the OEP curve, mark the 1-in-100 and 1-in-250 points with
   labelled dots, and draw the AAL as a horizontal reference line. Axis ticks at
   10/25/50/100/250/500.

   This chart is the single most important visual in the whole portfolio — it is what a CAT
   modeler looks at every day. Get the axes labelled properly ("Return Period (years)",
   "Loss ($M)"), and make it theme-aware with `var(--accent)` / `var(--text-muted)`.

**KPI row (4 tiles):** Total Insured Value · Average Annual Loss · PML (1-in-250) ·
Concentration Breaches.

Format currency compactly — `$1.2B`, `$847M`, `$3.4M`. Write one `formatMoney(value)` helper.

---

## Tests — `Portfolio.Tests/Services/CatRiskServiceTests.cs`

| Test | Assertion |
|---|---|
| `Accumulation_SingleLocation_RingEqualsOwnTiv` | `Assert.False(NativeAccelerated)` first |
| `Accumulation_TwoLocationsWithinRadius_EachRingIncludesBoth` | |
| `Accumulation_TwoLocationsOutsideRadius_RingsAreIndependent` | |
| `Accumulation_ExceedsConcentrationLimit_FlagsBreach` | `Breached == true`, `BreachCount == 1` |
| `Accumulation_NullRequest_ThrowsArgumentNullException` | |
| `Accumulation_ZeroRadius_ThrowsArgumentException` | |
| `Simulate_EventFarFromBook_ProducesZeroLoss` | epicenter beyond every footprint |
| `Simulate_DirectHit_LossBoundedByLimit` | gross ≤ `TIV * limitRate` |
| `Simulate_DeductibleExceedsDamage_ProducesZeroLoss` | clamp lower bound |
| `Simulate_HigherVulnerabilityAlpha_IncreasesLoss` | monotonicity |
| `Simulate_AverageAnnualLoss_EqualsRateWeightedSum` | hand-computed 2-event case |
| `Simulate_ExceedanceCurve_IsMonotonicallyDecreasing` | loss falls as return period falls |
| `Simulate_PmlEqualsOepAt250` | |
| `Simulate_SiteHazardZero_ProducesZeroLoss` | |
| `Simulate_ExceedsEvaluationLimit_ThrowsArgumentException` | |
| `Simulate_NullRequest_ThrowsArgumentNullException` | |
| `PolicyBook_IsDeterministic` | `Assert.Same(first, second)` on the materialized list |
| `PolicyBook_HasPlausibleValues` | bbox, TIV > 0, hazard in 0–1, ≥ 6 communities |

Controller tests in `SpatialComputeControllerTests.cs`: Ok path + `ArgumentException` →
`BadRequest` for both POSTs.

---

## Details page — Interview Discussion Points

Use these verbatim in section 6:

- Why does the EP curve use exceedance *rate* rather than a simple percentile of the loss
  distribution — and what breaks if events are not independent?
- Where is the boundary between OEP and AEP, and which one does a reinsurance treaty attach to?
- The vulnerability curve is `1 - exp(-α·i)`. What real-world properties does that shape have,
  and where does it fail?
- Ring accumulation is O(n²). At what book size does that stop being acceptable, and what
  would you replace it with — a grid index, an R-tree, or a k-d tree? What does each cost you?
- Deductibles here are percentage-of-TIV rather than flat dollars. Why is that the wildfire
  convention, and how would flat deductibles change the loss distribution?
- The native kernel returns per-event losses and the managed layer derives AAL and the EP
  curve. Why draw the boundary there instead of returning the finished curve from C++?
- How would you validate this model against actual loss experience?
