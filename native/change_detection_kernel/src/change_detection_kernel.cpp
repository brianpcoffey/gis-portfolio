#include "change_detection_kernel.h"

#include <cmath>
#include <vector>

namespace
{
	// Union-find root with full path compression. `parent` is a flat vector indexed by
	// provisional label; parent[0] is unused because label 0 means background.
	int find_root(std::vector<int>& parent, int label)
	{
		int root = label;
		while (parent[root] != root)
			root = parent[root];

		// Second walk compresses the path so later finds are effectively O(1).
		while (parent[label] != root)
		{
			const int next = parent[label];
			parent[label] = root;
			label = next;
		}
		return root;
	}

	void union_labels(std::vector<int>& parent, int a, int b)
	{
		const int rootA = find_root(parent, a);
		const int rootB = find_root(parent, b);
		if (rootA == rootB)
			return;

		// Attach the higher root to the lower so the smallest label in an equivalence
		// class is always the representative; this keeps first-appearance ordering stable.
		if (rootA < rootB)
			parent[rootB] = rootA;
		else
			parent[rootA] = rootB;
	}

	// One 3x3 erosion. Out-of-bounds neighbours read as background.
	void erode_once(const std::vector<unsigned char>& src, std::vector<unsigned char>& dst, int width, int height)
	{
		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				const int index = y * width + x;
				if (!src[index])
				{
					dst[index] = 0;
					continue;
				}

				unsigned char keep = 1;
				for (int dy = -1; dy <= 1 && keep; ++dy)
				{
					const int ny = y + dy;
					if (ny < 0 || ny >= height)
					{
						keep = 0;
						break;
					}

					for (int dx = -1; dx <= 1; ++dx)
					{
						const int nx = x + dx;
						if (nx < 0 || nx >= width || !src[ny * width + nx])
						{
							keep = 0;
							break;
						}
					}
				}

				dst[index] = keep;
			}
		}
	}

	// One 3x3 dilation. Out-of-bounds neighbours read as background.
	void dilate_once(const std::vector<unsigned char>& src, std::vector<unsigned char>& dst, int width, int height)
	{
		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				const int index = y * width + x;
				if (src[index])
				{
					dst[index] = 1;
					continue;
				}

				unsigned char hit = 0;
				for (int dy = -1; dy <= 1 && !hit; ++dy)
				{
					const int ny = y + dy;
					if (ny < 0 || ny >= height)
						continue;

					for (int dx = -1; dx <= 1; ++dx)
					{
						const int nx = x + dx;
						if (nx < 0 || nx >= width)
							continue;
						if (src[ny * width + nx])
						{
							hit = 1;
							break;
						}
					}
				}

				dst[index] = hit;
			}
		}
	}
}

extern "C" CHG_API int Change_ComputeCvaMagnitude(
	const double* epochA,
	const double* epochB,
	int width, int height, int bandCount,
	double* outMagnitude, int outputLength)
{
	try
	{
		if (!epochA || !epochB || !outMagnitude)
			return -1;
		if (width <= 0 || height <= 0 || bandCount <= 0)
			return -1;

		const long long pixels = static_cast<long long>(width) * static_cast<long long>(height);
		if (pixels > static_cast<long long>(1) << 30)
			return -1;
		if (outputLength < static_cast<int>(pixels))
			return -3;

		const int pixelCount = static_cast<int>(pixels);

		// Accumulate the squared band deltas band-by-band so the inner loop walks both
		// epochs contiguously; band-sequential layout makes that a pure streaming pass.
		for (int i = 0; i < pixelCount; ++i)
			outMagnitude[i] = 0.0;

		for (int b = 0; b < bandCount; ++b)
		{
			const double* a = epochA + static_cast<long long>(b) * pixelCount;
			const double* c = epochB + static_cast<long long>(b) * pixelCount;
			for (int i = 0; i < pixelCount; ++i)
			{
				const double delta = c[i] - a[i];
				outMagnitude[i] += delta * delta;
			}
		}

		for (int i = 0; i < pixelCount; ++i)
			outMagnitude[i] = std::sqrt(outMagnitude[i]);

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" CHG_API int Change_OtsuThreshold(
	const double* magnitude, int length,
	int binCount,
	double* outThreshold,
	int* outHistogram, int histogramCapacity)
{
	try
	{
		if (!magnitude || !outThreshold || !outHistogram)
			return -1;
		if (length <= 0 || binCount <= 1)
			return -1;
		if (histogramCapacity < binCount)
			return -3;

		double minValue = magnitude[0];
		double maxValue = magnitude[0];
		for (int i = 1; i < length; ++i)
		{
			if (magnitude[i] < minValue) minValue = magnitude[i];
			if (magnitude[i] > maxValue) maxValue = magnitude[i];
		}

		for (int b = 0; b < binCount; ++b)
			outHistogram[b] = 0;

		const double span = maxValue - minValue;
		if (!(span > 0.0))
		{
			// Uniform magnitude: everything lands in bin 0 and the threshold is the
			// single value present, so nothing is classified as changed.
			outHistogram[0] = length;
			*outThreshold = maxValue;
			return 0;
		}

		const double binWidth = span / binCount;
		for (int i = 0; i < length; ++i)
		{
			int bin = static_cast<int>((magnitude[i] - minValue) / binWidth);
			if (bin < 0) bin = 0;
			if (bin >= binCount) bin = binCount - 1;
			++outHistogram[bin];
		}

		// Bin centres, so mu0/mu1 are means of magnitude rather than of bin index.
		double totalWeight = 0.0;
		double totalMean = 0.0;
		for (int b = 0; b < binCount; ++b)
		{
			const double centre = minValue + (b + 0.5) * binWidth;
			totalWeight += static_cast<double>(outHistogram[b]);
			totalMean += static_cast<double>(outHistogram[b]) * centre;
		}

		double cumulativeWeight = 0.0;
		double cumulativeMean = 0.0;
		double bestVariance = -1.0;
		int bestBin = 0;

		for (int b = 0; b < binCount - 1; ++b)
		{
			const double centre = minValue + (b + 0.5) * binWidth;
			cumulativeWeight += static_cast<double>(outHistogram[b]);
			cumulativeMean += static_cast<double>(outHistogram[b]) * centre;

			const double w0 = cumulativeWeight / totalWeight;
			const double w1 = 1.0 - w0;
			if (w0 <= 0.0 || w1 <= 0.0)
				continue;

			const double mu0 = cumulativeMean / cumulativeWeight;
			const double mu1 = (totalMean - cumulativeMean) / (totalWeight - cumulativeWeight);
			const double diff = mu0 - mu1;
			const double variance = w0 * w1 * diff * diff;

			if (variance > bestVariance)
			{
				bestVariance = variance;
				bestBin = b;
			}
		}

		// Upper edge of the winning bin: class 0 is bins [0, bestBin], class 1 the rest.
		*outThreshold = minValue + (bestBin + 1) * binWidth;
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" CHG_API int Change_MorphologicalOpen(
	const unsigned char* mask,
	int width, int height, int iterations,
	unsigned char* outMask, int outputLength)
{
	try
	{
		if (!mask || !outMask)
			return -1;
		if (width <= 0 || height <= 0 || iterations < 0)
			return -1;

		const long long pixels = static_cast<long long>(width) * static_cast<long long>(height);
		if (pixels > static_cast<long long>(1) << 30)
			return -1;
		if (outputLength < static_cast<int>(pixels))
			return -3;

		const int pixelCount = static_cast<int>(pixels);

		std::vector<unsigned char> current(pixelCount);
		for (int i = 0; i < pixelCount; ++i)
			current[i] = mask[i] ? 1 : 0;

		if (iterations > 0)
		{
			std::vector<unsigned char> scratch(pixelCount);

			// erode x n, then dilate x n — a single open with an n-scaled structuring
			// element, not n consecutive opens.
			for (int it = 0; it < iterations; ++it)
			{
				erode_once(current, scratch, width, height);
				current.swap(scratch);
			}
			for (int it = 0; it < iterations; ++it)
			{
				dilate_once(current, scratch, width, height);
				current.swap(scratch);
			}
		}

		for (int i = 0; i < pixelCount; ++i)
			outMask[i] = current[i];

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" CHG_API int Change_LabelComponents(
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
	int blobCapacity)
{
	try
	{
		if (!mask || !outLabels || !outBlobAreas || !outBlobCentroidX || !outBlobCentroidY ||
			!outBlobMeanMagnitude || !outBlobMinX || !outBlobMinY || !outBlobMaxX || !outBlobMaxY || !magnitude)
			return -1;
		if (width <= 0 || height <= 0 || blobCapacity < 0)
			return -1;

		const long long pixels = static_cast<long long>(width) * static_cast<long long>(height);
		if (pixels > static_cast<long long>(1) << 30)
			return -1;
		if (labelsLength < static_cast<int>(pixels))
			return -3;

		const int pixelCount = static_cast<int>(pixels);

		std::vector<int> provisional(pixelCount, 0);
		std::vector<int> parent;
		parent.push_back(0); // label 0 = background, never a root query

		// ── Pass one: provisional labels + equivalences ──────────────────────
		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				const int index = y * width + x;
				if (!mask[index])
					continue;

				// The four already-visited 8-connected neighbours: NW, N, NE, W.
				int neighbours[4];
				int neighbourCount = 0;

				if (y > 0)
				{
					if (x > 0)
					{
						const int label = provisional[(y - 1) * width + (x - 1)];
						if (label) neighbours[neighbourCount++] = label;
					}
					{
						const int label = provisional[(y - 1) * width + x];
						if (label) neighbours[neighbourCount++] = label;
					}
					if (x + 1 < width)
					{
						const int label = provisional[(y - 1) * width + (x + 1)];
						if (label) neighbours[neighbourCount++] = label;
					}
				}
				if (x > 0)
				{
					const int label = provisional[y * width + (x - 1)];
					if (label) neighbours[neighbourCount++] = label;
				}

				if (neighbourCount == 0)
				{
					const int fresh = static_cast<int>(parent.size());
					parent.push_back(fresh);
					provisional[index] = fresh;
					continue;
				}

				int smallest = neighbours[0];
				for (int n = 1; n < neighbourCount; ++n)
				{
					if (neighbours[n] < smallest)
						smallest = neighbours[n];
				}

				provisional[index] = smallest;
				for (int n = 0; n < neighbourCount; ++n)
					union_labels(parent, smallest, neighbours[n]);
			}
		}

		// ── Pass two: resolve, compact, accumulate ───────────────────────────
		const int provisionalCount = static_cast<int>(parent.size());
		std::vector<int> compact(provisionalCount, 0);
		int componentCount = 0;

		for (int i = 0; i < pixelCount; ++i)
			outLabels[i] = 0;

		std::vector<double> sumX;
		std::vector<double> sumY;
		std::vector<double> sumMagnitude;
		std::vector<int> area;
		std::vector<int> minX, minY, maxX, maxY;

		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				const int index = y * width + x;
				const int label = provisional[index];
				if (!label)
					continue;

				const int root = find_root(parent, label);
				int id = compact[root];
				if (id == 0)
				{
					++componentCount;
					if (componentCount > blobCapacity)
						return -6;

					id = componentCount;
					compact[root] = id;
					sumX.push_back(0.0);
					sumY.push_back(0.0);
					sumMagnitude.push_back(0.0);
					area.push_back(0);
					minX.push_back(x);
					minY.push_back(y);
					maxX.push_back(x);
					maxY.push_back(y);
				}

				const int slot = id - 1;
				outLabels[index] = id;
				sumX[slot] += static_cast<double>(x);
				sumY[slot] += static_cast<double>(y);
				sumMagnitude[slot] += magnitude[index];
				++area[slot];
				if (x < minX[slot]) minX[slot] = x;
				if (y < minY[slot]) minY[slot] = y;
				if (x > maxX[slot]) maxX[slot] = x;
				if (y > maxY[slot]) maxY[slot] = y;
			}
		}

		for (int c = 0; c < componentCount; ++c)
		{
			const double count = static_cast<double>(area[c]);
			outBlobAreas[c] = area[c];
			outBlobCentroidX[c] = count > 0.0 ? sumX[c] / count : 0.0;
			outBlobCentroidY[c] = count > 0.0 ? sumY[c] / count : 0.0;
			outBlobMeanMagnitude[c] = count > 0.0 ? sumMagnitude[c] / count : 0.0;
			outBlobMinX[c] = minX[c];
			outBlobMinY[c] = minY[c];
			outBlobMaxX[c] = maxX[c];
			outBlobMaxY[c] = maxY[c];
		}

		return componentCount;
	}
	catch (...)
	{
		return -99;
	}
}
