#pragma once

#if defined(_WIN32)
	#define CHG_API __declspec(dllexport)
#else
	#define CHG_API __attribute__((visibility("default")))
#endif

// Multitemporal raster change detection.
//
// Status codes follow the portfolio convention:
//    0  success
//   -1  null or invalid argument
//   -3  output buffer too small
//   -6  more connected components than blobCapacity (see Change_LabelComponents)
//  -99  unhandled exception
//
// A function that returns a count returns >= 0 on success and negative on error.
//
// This kernel carries no structs: every parameter is a flat, contiguous primitive
// buffer, which is the whole point of doing raster work across a P/Invoke boundary.

extern "C"
{
	// Change Vector Analysis magnitude between two co-registered multi-band epochs.
	// Both rasters are row-major, band-sequential: band b starts at b * width * height.
	//   magnitude[i] = sqrt( sum_b (epochB[b][i] - epochA[b][i])^2 )
	// CVA is preferred over differencing a single band because the per-pixel difference
	// vector across all bands is insensitive to which band carries the change, and a
	// uniform illumination offset moves every band together rather than saturating one.
	// `outputLength` must be at least width * height.
	// Returns 0 on success, negative on error.
	CHG_API int Change_ComputeCvaMagnitude(
		const double* epochA,
		const double* epochB,
		int width, int height, int bandCount,
		double* outMagnitude, int outputLength);

	// Otsu's method: the threshold maximizing between-class variance over a
	// binCount-bin histogram of the magnitude raster.
	//
	// The histogram spans [min, max] of the magnitude raster. Bin b covers
	// [min + b*w, min + (b+1)*w) with w = (max - min) / binCount; the final bin is
	// closed on the right. Between-class variance for a split after bin b is
	// w0*w1*(mu0 - mu1)^2, computed in a single pass over cumulative weight and
	// cumulative weighted mean. The returned threshold is the upper edge of the
	// winning bin, so "changed" is magnitude > threshold.
	//
	// Degenerate case: when every magnitude is identical the threshold is that value,
	// which classifies nothing as changed rather than everything.
	//
	// `histogramCapacity` must be at least binCount.
	// Returns 0 on success, negative on error.
	CHG_API int Change_OtsuThreshold(
		const double* magnitude, int length,
		int binCount,
		double* outThreshold,
		int* outHistogram, int histogramCapacity);

	// Morphological open with a 3x3 (8-connected) structuring element.
	//
	// IMPORTANT: with iterations > 1 this is erode x n followed by dilate x n, NOT
	// (erode + dilate) x n. The former removes structures narrower than n pixels and
	// restores everything larger; the latter is n independent opens and removes far less.
	//
	// Border convention: pixels outside the raster read as background. Erosion therefore
	// eats inward from the raster edge, and a blob touching the border loses its border
	// row. That is the conservative choice for a change detector — an edge-clipped
	// detection is a partial detection, not a fabricated one.
	//
	// Input values are treated as binary: any non-zero byte is foreground.
	// `outputLength` must be at least width * height. In-place aliasing is not supported.
	// Returns 0 on success, negative on error.
	CHG_API int Change_MorphologicalOpen(
		const unsigned char* mask,
		int width, int height, int iterations,
		unsigned char* outMask, int outputLength);

	// Two-pass union-find connected-component labelling, 8-connectivity.
	//
	// Pass one assigns provisional labels scanning row-major and unions the labels of
	// the four already-visited neighbours (NW, N, NE, W). Pass two resolves each
	// provisional label to its root with path compression, compacts the surviving roots
	// into ids 1..N in first-appearance order, and accumulates per-blob area, centroid
	// sums, mean magnitude, and bounding box.
	//
	// outLabels is per-pixel (0 = background, 1..N = component id) and must be at least
	// width * height long. Per-blob statistics are written in component-id order, so
	// element (id - 1) of each blob array belongs to component id.
	//
	// Every per-blob output array must hold at least blobCapacity elements. If the raster
	// contains more than blobCapacity components the function returns -6 rather than
	// truncating: a change detector that quietly drops detections is worse than one that
	// fails loudly.
	//
	// `magnitude` is parallel to `mask` and supplies the values averaged into
	// outBlobMeanMagnitude.
	//
	// Returns the number of components found (>= 0), negative on error.
	CHG_API int Change_LabelComponents(
		const unsigned char* mask,
		int width, int height,
		int* outLabels, int labelsLength,
		int* outBlobAreas,
		double* outBlobCentroidX,
		double* outBlobCentroidY,
		double* outBlobMeanMagnitude,
		int* outBlobMinX,
		int* outBlobMinY,
		int* outBlobMaxX,
		int* outBlobMaxY,
		const double* magnitude,
		int blobCapacity);
}
