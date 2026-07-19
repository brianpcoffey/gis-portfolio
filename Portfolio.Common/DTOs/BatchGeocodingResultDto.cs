namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Geocoding outcome for a single address within a batch geocoding job.
    /// </summary>
    public class BatchGeocodingResultDto
    {
        /// <summary>The address exactly as it was submitted in the batch.</summary>
        public string OriginalAddress { get; set; } = string.Empty;

        /// <summary>True when the address was successfully matched to a location.</summary>
        public bool Matched { get; set; }

        /// <summary>The candidate address the geocoder matched, when matched.</summary>
        public string MatchedAddress { get; set; } = string.Empty;

        /// <summary>Match quality score, typically 0-100 where higher is a better match.</summary>
        public double Score { get; set; }

        /// <summary>WGS84 latitude of the matched location, or null when unmatched.</summary>
        public double? Latitude { get; set; }

        /// <summary>WGS84 longitude of the matched location, or null when unmatched.</summary>
        public double? Longitude { get; set; }
    }
}
