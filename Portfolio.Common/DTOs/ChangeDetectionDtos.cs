namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// One labelled ground-truth change planted in the synthetic scene, used to score
    /// detections. Coordinates are inclusive pixel indices.
    /// </summary>
    public class GroundTruthBoxDto
    {
        /// <summary>Human-readable name of the planted change (e.g. "Burn scar").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Leftmost pixel column of the change, inclusive.</summary>
        public int MinX { get; set; }

        /// <summary>Topmost pixel row of the change, inclusive.</summary>
        public int MinY { get; set; }

        /// <summary>Rightmost pixel column of the change, inclusive.</summary>
        public int MaxX { get; set; }

        /// <summary>Bottom-most pixel row of the change, inclusive.</summary>
        public int MaxY { get; set; }
    }

    /// <summary>
    /// A synthetic two-epoch, multi-band multitemporal stack over a single AOI, with the
    /// planted changes exposed as ground truth so detections can be scored.
    /// </summary>
    public class ChangeSceneDto
    {
        /// <summary>Raster width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Raster height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Number of spectral bands per epoch (Red, Green, NIR, SWIR).</summary>
        public int BandCount { get; set; }

        /// <summary>Display name of the AOI.</summary>
        public string SceneName { get; set; } = string.Empty;

        /// <summary>Names of the bands in stack order.</summary>
        public List<string> BandNames { get; set; } = [];

        /// <summary>Epoch A reflectance, row-major and band-sequential: band b starts at b * width * height.</summary>
        public List<double> EpochA { get; set; } = [];

        /// <summary>Epoch B reflectance, same layout as <see cref="EpochA"/>.</summary>
        public List<double> EpochB { get; set; } = [];

        /// <summary>Planted changes, as inclusive pixel bounding boxes.</summary>
        public List<GroundTruthBoxDto> GroundTruth { get; set; } = [];

        /// <summary>Gaussian noise standard deviation applied to both epochs, in reflectance units.</summary>
        public double NoiseLevel { get; set; }
    }

    /// <summary>
    /// A change detection run over a co-registered two-epoch multi-band stack.
    /// </summary>
    public class DetectRequestDto
    {
        /// <summary>Raster width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Raster height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>Number of spectral bands per epoch.</summary>
        public int BandCount { get; set; }

        /// <summary>Epoch A reflectance, row-major and band-sequential.</summary>
        public List<double> EpochA { get; set; } = [];

        /// <summary>Epoch B reflectance, row-major and band-sequential.</summary>
        public List<double> EpochB { get; set; } = [];

        /// <summary>Either "otsu" (automatic) or "manual".</summary>
        public string ThresholdMode { get; set; } = "otsu";

        /// <summary>CVA magnitude threshold used when <see cref="ThresholdMode"/> is "manual".</summary>
        public double ManualThreshold { get; set; }

        /// <summary>Morphological open iterations, 0 to 5. Zero disables speckle removal.</summary>
        public int OpenIterations { get; set; }

        /// <summary>Blobs smaller than this pixel area are discarded as speckle.</summary>
        public int MinBlobArea { get; set; }
    }

    /// <summary>
    /// One detected change blob: a connected component of the change mask.
    /// </summary>
    public class ChangeBlobDto
    {
        /// <summary>Rank of the detection, 1 = largest by area.</summary>
        public int Id { get; set; }

        /// <summary>Number of pixels in the blob.</summary>
        public int Area { get; set; }

        /// <summary>Mean pixel column of the blob.</summary>
        public double CentroidX { get; set; }

        /// <summary>Mean pixel row of the blob.</summary>
        public double CentroidY { get; set; }

        /// <summary>Mean CVA magnitude over the blob's pixels.</summary>
        public double MeanMagnitude { get; set; }

        /// <summary>
        /// Mean magnitude divided by the scene's maximum magnitude, clamped to 0–1.
        /// This is a relative heuristic for ranking detections, NOT a calibrated
        /// probability of change — nothing here is trained against labelled data.
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>Leftmost pixel column of the blob, inclusive.</summary>
        public int MinX { get; set; }

        /// <summary>Topmost pixel row of the blob, inclusive.</summary>
        public int MinY { get; set; }

        /// <summary>Rightmost pixel column of the blob, inclusive.</summary>
        public int MaxX { get; set; }

        /// <summary>Bottom-most pixel row of the blob, inclusive.</summary>
        public int MaxY { get; set; }
    }

    /// <summary>
    /// Result of the change detection pipeline: CVA magnitude, the thresholded mask, the
    /// magnitude histogram, and the ranked change detections.
    /// </summary>
    public class DetectResultDto
    {
        /// <summary>True when the native C++ kernel served the request.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Raster width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Raster height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>CVA magnitude threshold that produced the mask.</summary>
        public double Threshold { get; set; }

        /// <summary>Either "otsu" or "manual", echoing how the threshold was chosen.</summary>
        public string ThresholdMode { get; set; } = string.Empty;

        /// <summary>Per-pixel CVA magnitude, row-major.</summary>
        public List<double> Magnitude { get; set; } = [];

        /// <summary>Binary change mask after morphological open, row-major. 1 = changed.</summary>
        public List<byte> Mask { get; set; } = [];

        /// <summary>Magnitude histogram used by Otsu, spanning <see cref="HistogramMin"/> to <see cref="HistogramMax"/>.</summary>
        public List<int> Histogram { get; set; } = [];

        /// <summary>Lowest CVA magnitude in the scene, the histogram's lower edge.</summary>
        public double HistogramMin { get; set; }

        /// <summary>Highest CVA magnitude in the scene, the histogram's upper edge.</summary>
        public double HistogramMax { get; set; }

        /// <summary>Detections surviving the minimum-area filter, sorted by area descending.</summary>
        public List<ChangeBlobDto> Blobs { get; set; } = [];

        /// <summary>Number of mask pixels classified as changed after the open.</summary>
        public int ChangedPixels { get; set; }

        /// <summary>Changed pixels as a percentage of the raster.</summary>
        public double ChangedPercent { get; set; }

        /// <summary>Connected components found before the minimum-area filter was applied.</summary>
        public int BlobsBeforeFiltering { get; set; }
    }
}
