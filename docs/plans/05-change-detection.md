# Plan 5 — Raster Change Detection

**Industry:** Defense / geospatial intelligence, and civil remote sensing
**Kernel:** `change_detection_kernel`
**Dependencies:** none
**Target employers:** Maxar, Planet, BlackSky, Palantir, Anduril, L3Harris, BAE, Leidos, Booz Allen — and civil: USGS, NOAA, CAL FIRE, Descartes Labs, Esri imagery

---

## The pitch

Two satellite passes over the same ground, weeks apart. **What changed?**

That question drives new-construction monitoring, deforestation tracking, flood extent
mapping, post-disaster damage assessment, and every "tip and cue" workflow in commercial
GEOINT. The analyst's job is to find the handful of pixels that matter in a hundred million
that don't.

The pipeline is a genuinely classic remote-sensing chain, and every stage is dense array
work over contiguous memory — the cleanest native-code justification in the set:

```
two multi-band epochs
  → Change Vector Analysis magnitude      (per-pixel across bands)
  → Otsu automatic threshold             (histogram, no magic number)
  → morphological open                   (kill speckle)
  → connected-component labelling        (blobs with area + centroid)
  → ranked change detections
```

**Frame it civil-forward** — new construction, burn scars, reservoir drawdown, flood extent.
Defense employers read that correctly without the portfolio needing to be about weapons, and
it stays honest about what the tool does.

---

## Glossary

| Term | Meaning |
|---|---|
| **Epoch** | One acquisition date. This model uses two. |
| **Band** | A spectral channel (red, NIR, SWIR…). |
| **Multitemporal stack** | The same scene across epochs, co-registered. |
| **Co-registration** | Aligning epochs pixel-to-pixel. Assumed here; call that out. |
| **CVA** | Change Vector Analysis — magnitude of the per-pixel difference vector across bands. |
| **NDVI** | `(NIR − Red) / (NIR + Red)`. Vegetation index; drops sharply after fire or clearing. |
| **Otsu's method** | Automatic threshold that maximizes between-class variance of the histogram. |
| **Change mask** | Binary raster: 1 = changed. |
| **Speckle** | Isolated false-positive pixels from noise or misregistration. |
| **Morphological open** | Erode then dilate. Removes speckle while preserving larger shapes. |
| **Connected components** | Grouping adjacent mask pixels into discrete blobs. |
| **8-connectivity** | Diagonal neighbors count as connected. |
| **AOI** | Area of Interest. |
| **Detection** | One reported change blob with area, centroid, and confidence. |

---

## Native ABI — `native/change_detection_kernel/include/change_detection_kernel.h`

```c
extern "C"
{
	// Change Vector Analysis magnitude between two co-registered multi-band epochs.
	// Both rasters are row-major, band-sequential: band b starts at b * width * height.
	// magnitude[i] = sqrt( sum_b (epochB[b][i] - epochA[b][i])^2 )
	// Returns 0 on success, negative on error.
	CHG_API int Change_ComputeCvaMagnitude(
		const double* epochA,
		const double* epochB,
		int width, int height, int bandCount,
		double* outMagnitude, int outputLength);

	// Otsu's method: the threshold maximizing between-class variance over a
	// binCount-bin histogram of the magnitude raster.
	// Writes the chosen threshold and the histogram (for plotting).
	// Returns 0 on success, negative on error.
	CHG_API int Change_OtsuThreshold(
		const double* magnitude, int length,
		int binCount,
		double* outThreshold,
		int* outHistogram, int histogramCapacity);

	// Morphological open (erode then dilate) with a 3x3 structuring element.
	// Returns 0 on success, negative on error.
	CHG_API int Change_MorphologicalOpen(
		const unsigned char* mask,
		int width, int height, int iterations,
		unsigned char* outMask, int outputLength);

	// Two-pass union-find connected-component labelling, 8-connectivity.
	// outLabels is per-pixel (0 = background, 1..N = component id).
	// Per-blob statistics are written in component-id order.
	// Returns the number of components found (>= 0), negative on error.
	CHG_API int Change_LabelComponents(
		const unsigned char* mask,
		int width, int height,
		int* outLabels, int labelsLength,
		int* outBlobAreas,
		double* outBlobCentroidX,
		double* outBlobCentroidY,
		double* outBlobMeanMagnitude,
		const double* magnitude,
		int blobCapacity);
}
```

Additional status code: **`-6` = more components than `blobCapacity`.** Return it rather
than truncating silently — a change detector that quietly drops detections is worse than one
that fails.

### Implementation notes

- **Otsu:** build the histogram, then compute cumulative sums of weight and weighted mean in
  one pass. Between-class variance is `w0·w1·(μ0 − μ1)²`; take the bin maximizing it. This is
  ~30 lines and it is the most elegant thing in the whole portfolio — get it right.
- **CCL:** classic two-pass with union-find. First pass assigns provisional labels and records
  equivalences; second pass resolves them via find-with-path-compression and accumulates area,
  centroid sums, and mean magnitude. Use a flat `std::vector<int>` parent array — this is
  exactly the workload where native beats a managed dictionary-based approach.
- **Morphological open** with `iterations > 1` means erode×n then dilate×n, not
  (erode+dilate)×n. Get the order right; document it in the header.
- Bounds-check every neighbor access at the raster border. Off-by-one here produces a
  plausible-looking wrong answer, which is the worst kind.

---

## Synthetic scene — `Portfolio.Services/Data/SyntheticChangeScene.cs`

Deterministic LCG, `internal static class` with `Build(int width, int height)`.

**A 4-band, 2-epoch stack at 256×256** (adjustable to 128 or 512 from the UI). Bands modeled
loosely as Red / Green / NIR / SWIR so NDVI is computable and the vocabulary is honest.

Base scene: fractal-ish terrain via value noise, a river, a road grid, vegetated blocks, and
bare ground. Then **plant four labelled changes in epoch B** so the demo has ground truth:

| Change | Shape | Signature |
|---|---|---|
| New subdivision | ~18×14 rectangle | vegetation → bright bare soil; NDVI drops |
| Burn scar | irregular blob ~30 px across | sharp NIR drop, SWIR rise |
| Reservoir drawdown | crescent along the river | water → sediment |
| New solar array | ~22×22 rectangle | uniform low-albedo, very sharp edges |

Add **Gaussian noise to both epochs** (adjustable from the UI) so Otsu has real work to do and
morphological open has speckle to remove. Also add a small global brightness offset to epoch B
— real acquisitions differ in illumination, and it makes the case for CVA over naive
differencing.

Expose the ground-truth rectangles so the page can score detections (see below).

---

## DTOs — `Portfolio.Common/DTOs/ChangeDetectionDtos.cs`

```
ChangeSceneDto        Width, Height, BandCount, SceneName,
                      EpochA (List<double>), EpochB (List<double>),
                      GroundTruth (List<GroundTruthBoxDto>)
GroundTruthBoxDto     Label, MinX, MinY, MaxX, MaxY

DetectRequestDto      Width, Height, BandCount, EpochA, EpochB,
                      ThresholdMode, ManualThreshold, OpenIterations, MinBlobArea
ChangeBlobDto         Id, Area, CentroidX, CentroidY, MeanMagnitude, Confidence,
                      MinX, MinY, MaxX, MaxY
DetectResultDto       NativeAccelerated, Width, Height,
                      Threshold, ThresholdMode,
                      Magnitude (List<double>), Mask (List<byte>),
                      Histogram (List<int>), HistogramMin, HistogramMax,
                      Blobs (List<ChangeBlobDto>), ChangedPixels, ChangedPercent,
                      BlobsBeforeFiltering
```

`ThresholdMode` is `"otsu"` or `"manual"`. `Confidence` is
`meanMagnitude / maxMagnitude` clamped to 0–1 — label it plainly as a heuristic, not a
calibrated probability, both in the DTO doc comment and in the UI.

`Magnitude` and `Mask` at 256×256 are 65,536 elements each. That is a ~1.5 MB response —
acceptable, but set the request limit accordingly and cap `Width`/`Height` at 512.

---

## Service — `ChangeDetectionService` / `IChangeDetectionService`

```csharp
Task<ChangeSceneDto> GetSceneAsync(int width, int height, CancellationToken cancellationToken = default);
Task<DetectResultDto> DetectAsync(DetectRequestDto request, CancellationToken cancellationToken = default);
```

Limits:

```csharp
private const int MaxDimension = 512;
private const int MaxBands = 8;
private const int MaxPixels = 262_144;   // 512 * 512
private const int MaxBlobs = 5_000;
private const int MaxOpenIterations = 5;
```

Pipeline in `DetectAsync`:

1. CVA magnitude (native or managed)
2. Threshold — Otsu (native or managed) or the caller's manual value
3. Binarize into the mask
4. Morphological open if `openIterations > 0`
5. Connected components
6. Drop blobs under `minBlobArea`, record `BlobsBeforeFiltering`
7. Sort remaining blobs by area descending, renumber ids from 1

Validation messages:

| Condition | Message |
|---|---|
| `width <= 0` or `height <= 0` | `"Raster dimensions must be greater than zero."` |
| either `> MaxDimension` | `$"Raster dimensions are limited to {MaxDimension}."` |
| `width * height > MaxPixels` | `$"Rasters are limited to {MaxPixels} pixels."` |
| `bandCount < 1` or `> MaxBands` | `$"Band count must be between 1 and {MaxBands}."` |
| epoch length `!= width*height*bandCount` | `"Epoch raster length must equal width * height * bandCount."` |
| epoch lengths differ | `"Both epochs must have identical dimensions."` |
| non-finite raster value | `"Raster values must be finite."` |
| `openIterations` outside 0..5 | `$"Open iterations must be between 0 and {MaxOpenIterations}."` |
| `minBlobArea < 0` | `"Minimum blob area cannot be negative."` |
| manual mode with non-finite/negative threshold | `"Manual threshold must be a finite non-negative value."` |
| unknown `thresholdMode` | `"Threshold mode must be 'otsu' or 'manual'."` |

---

## API — `ChangeDetectionController`, route `api/v{version:apiVersion}/change`

| Method | Path | Returns |
|---|---|---|
| `GET` | `/scene?width=256&height=256` | `ChangeSceneDto` |
| `POST` | `/detect` | `DetectResultDto` |

`[RequestSizeLimit(16_000_000)]` — two 4-band 512×512 `double` epochs is a large body.
`[EnableRateLimiting("expensive")]`.

```js
change: {
    scene : BASE + "/change/scene",
    detect: BASE + "/change/detect"
},
```

---

## UI — `/Projects/Change`

Icon: `fa-solid fa-satellite`. Badge: "C++ Change Kernel".

**Self-contained raster rendering.** Reuse `.raster-grid` / `.raster-cell` from
`spatialcompute.css` (the Terrain Analyzer already uses them), or draw to a `<canvas>` if
256×256 divs prove too slow — **prefer canvas at this size**, and note the choice on the
Details page as a real rendering-performance decision.

**Left column — Detection Controls**
- Scene size — select 128 / 256 / 512
- Noise level — range slider 0–0.3
- Threshold mode — radio: *Otsu (automatic)* / *Manual*
- Manual threshold — slider, enabled only in manual mode
- Open iterations — number input 0–5
- Minimum blob area — number input, default 12
- **Detect Changes** / **New Scene**
- Detection list as `.geo-cell-item` rows: rank, area in pixels, centroid, confidence bar.
  Clicking a row highlights that blob on the raster.
- `<pre class="sample-json">` echo

**Right column — three views**

1. **Before/after swipe.** Two stacked canvases with a draggable vertical divider revealing
   epoch B over epoch A. Render as false-color RGB from bands (NIR, Red, Green) — the
   standard vegetation-forward composite, and naming it that way is a credibility detail.
   This interaction is the single most compelling thing on the page; build it properly with
   a pointer-drag handler, not a slider input.

2. **Change mask overlay.** The detected mask over epoch B, with each blob outlined by its
   bounding box and numbered by rank. Ground-truth boxes drawn as dashed outlines so hits and
   misses are visible at a glance.

3. **Otsu histogram.** SVG bar chart of the magnitude histogram with a vertical line at the
   chosen threshold. Watching that line move as noise increases is the best explanation of
   what Otsu does that anyone will ever see.

**KPI row:** Detections · Changed Area (%) · Threshold · Largest Blob.

**Scoring against ground truth** (small addition, large payoff): compute how many planted
changes were recovered — a detection counts as a hit if its centroid falls inside a
ground-truth box. Show "3 of 4 recovered" as a KPI. It turns a demo into an evaluation, and
it gives the Details page something honest to say about false positives and misses.

---

## Tests — `Portfolio.Tests/Services/ChangeDetectionServiceTests.cs`

Hand-built tiny rasters — an 8×8 with one 2×2 planted square is enough for most cases.

| Test | Assertion |
|---|---|
| `Detect_IdenticalEpochs_ProducesNoChange` | `Assert.False(NativeAccelerated)` first; zero blobs |
| `Detect_SingleSquare_FindsOneBlob` | area == 4, centroid at the square's center |
| `Detect_TwoSeparateSquares_FindsTwoBlobs` | |
| `Detect_DiagonallyAdjacentPixels_AreOneBlob` | 8-connectivity |
| `Detect_BlobAreaMatchesPixelCount` | |
| `Detect_CentroidIsPixelMean` | hand-computed |
| `Detect_MinBlobArea_FiltersSmallBlobs` | `BlobsBeforeFiltering > Blobs.Count` |
| `Detect_BlobsAreSortedByAreaDescending` | |
| `Detect_MorphologicalOpen_RemovesSinglePixelSpeckle` | isolated pixel gone, square survives |
| `Detect_MorphologicalOpen_ZeroIterations_IsNoOp` | |
| `Otsu_BimodalHistogram_ThresholdFallsBetweenModes` | the key correctness test |
| `Otsu_UniformMagnitude_ReturnsStableThreshold` | degenerate case does not crash |
| `Detect_ManualThreshold_OverridesOtsu` | `ThresholdMode == "manual"` |
| `Detect_HigherThreshold_ProducesFewerChangedPixels` | monotonicity |
| `Detect_CvaMagnitude_IsEuclideanAcrossBands` | 2-band hand-computed case |
| `Detect_MismatchedEpochLengths_ThrowsArgumentException` | |
| `Detect_ZeroWidth_ThrowsArgumentException` | |
| `Detect_TooManyPixels_ThrowsArgumentException` | |
| `Detect_NonFiniteValue_ThrowsArgumentException` | |
| `Detect_NullRequest_ThrowsArgumentNullException` | |
| `Scene_IsDeterministic` | same scene twice |
| `Scene_PlantedChangesAreDetectable` | end-to-end: every ground-truth box recovered on a clean, noiseless scene |

That last test is the one worth writing carefully — it is an actual accuracy assertion, not
a smoke test.

---

## Details page — Interview Discussion Points

- Why Change Vector Analysis instead of simply differencing one band? What does CVA give you
  when the two acquisitions have different illumination?
- Otsu picks a threshold by maximizing between-class variance. What assumption does that make
  about the histogram, and what happens when only 0.5% of the scene actually changed?
- The pipeline assumes the epochs are co-registered. What does a one-pixel misregistration do
  to the change mask, and how would you detect that it happened?
- Connected-component labelling here is two-pass union-find. What is the alternative, and why
  is union-find the right call for a raster?
- Morphological open removes speckle but also erodes real small changes. How do you choose
  the structuring element size, and what do you lose?
- `Confidence` is mean magnitude over max magnitude. Why is that not a probability, and what
  would you need to produce a calibrated one?
- At 512×512×4 bands this is 4 MB of doubles crossing the P/Invoke boundary per call. Where
  does that stop scaling, and what would you change — tiling, floats instead of doubles,
  memory-mapped rasters, or moving the compute to the client?
- How would this change if you had 12 epochs instead of 2?
