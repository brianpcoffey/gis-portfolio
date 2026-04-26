using Microsoft.Extensions.Logging;
using Portfolio.Common.ArcGis;
using Portfolio.Common.DTOs;
using Portfolio.Common.Enums;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Portfolio.Services.Services
{
    public class AddressStandardizationService : IAddressStandardizationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AddressStandardizationService> _logger;

        private const string GeocodeUrl =
            "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates";

        // Full suffix abbreviation → full-word lookup table.
        private static readonly Dictionary<string, string> SuffixMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["St"]   = "Street",
            ["Ave"]  = "Avenue",
            ["Blvd"] = "Boulevard",
            ["Dr"]   = "Drive",
            ["Rd"]   = "Road",
            ["Ln"]   = "Lane",
            ["Ct"]   = "Court",
            ["Pl"]   = "Place",
            ["Hwy"]  = "Highway"
        };

        // Known US state abbreviations for validation.
        private static readonly HashSet<string> UsStateAbbreviations = new(StringComparer.OrdinalIgnoreCase)
        {
            "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA",
            "HI","ID","IL","IN","IA","KS","KY","LA","ME","MD",
            "MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ",
            "NM","NY","NC","ND","OH","OK","OR","PA","RI","SC",
            "SD","TN","TX","UT","VT","VA","WA","WV","WI","WY",
            "DC","PR","VI","GU","MP","AS"
        };

        // Regex patterns used during parsing.
        private static readonly Regex ZipPattern        = new(@"\b(\d{5}(?:-\d{4})?)\b", RegexOptions.Compiled);
        private static readonly Regex StateZipPattern   = new(@"\b([A-Za-z]{2})\s+(\d{5}(?:-\d{4})?)\b", RegexOptions.Compiled);
        private static readonly Regex HouseNumberPattern = new(@"^(\d+[A-Za-z]?)\s+", RegexOptions.Compiled);
        private static readonly Regex UnitPattern       = new(@"\b(?:Apt|Suite|Ste|Unit|#)\s*([A-Za-z0-9-]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NormalizeSpaces   = new(@"\s+", RegexOptions.Compiled);

        public AddressStandardizationService(HttpClient httpClient, ILogger<AddressStandardizationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // Parses a raw freeform address into structured, standardized components.
        public Task<AddressParsedDto> ParseAsync(string rawAddress, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawAddress, nameof(rawAddress));
            var result = ParseAddress(rawAddress);
            return Task.FromResult(result);
        }

        // Parses, then geocodes the standardized address against ArcGIS.
        // Falls back to City+State+ZIP if the full-address score is below 75.
        public async Task<AddressValidationResultDto> ValidateAsync(string rawAddress, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawAddress, nameof(rawAddress));

            var parsed = ParseAddress(rawAddress);

            // First pass: geocode the full standardized address.
            var (matchedAddress, score) = await GeocodeAddressAsync(parsed.StandardizedAddress, cancellationToken);

            // Fallback: if score < 75, retry with City+State+ZIP only.
            if (score < 75.0)
            {
                var fallbackQuery = BuildCityStateZipQuery(parsed);
                if (!string.IsNullOrWhiteSpace(fallbackQuery))
                {
                    _logger.LogWarning(
                        "Full-address geocode score {Score} below 75 for \"{Address}\". Retrying with City+State+ZIP.",
                        score, parsed.StandardizedAddress);

                    var (fallbackMatch, fallbackScore) = await GeocodeAddressAsync(fallbackQuery, cancellationToken);
                    if (fallbackScore >= 50.0)
                    {
                        matchedAddress = fallbackMatch;
                        score = fallbackScore;
                    }
                }
            }

            var tier = DetermineConfidenceTier(score);

            return new AddressValidationResultDto
            {
                Parsed = parsed,
                MatchedAddress = matchedAddress,
                Score = score,
                ConfidenceTier = tier
            };
        }

        // Calls the ArcGIS findAddressCandidates endpoint for a single address string.
        // Returns ("", 0) when no candidates are returned.
        private async Task<(string MatchedAddress, double Score)> GeocodeAddressAsync(
            string address, CancellationToken cancellationToken)
        {
            var encoded = Uri.EscapeDataString(address);
            var url = $"{GeocodeUrl}?singleLine={encoded}&f=json&maxLocations=1";

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var parsed = await response.Content.ReadFromJsonAsync<ArcGisGeocodeResponse>(
                    cancellationToken: cancellationToken);

                var top = parsed?.Candidates?.FirstOrDefault();
                if (top is null)
                    return (string.Empty, 0.0);

                return (top.Address ?? string.Empty, top.Score);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error geocoding address \"{Address}\"", address);
                return (string.Empty, 0.0);
            }
        }

        // Core parsing logic. All string manipulation starts from a normalised lowercase copy,
        // but casing is restored for suffix expansion and state abbreviations.
        private static AddressParsedDto ParseAddress(string rawAddress)
        {
            // Normalise whitespace; keep a working copy (preserves original casing for readback).
            var working = NormalizeSpaces.Replace(rawAddress.Trim(), " ");

            var houseNumber = string.Empty;
            var streetName  = string.Empty;
            var streetSuffix = string.Empty;
            var unit        = string.Empty;
            var city        = string.Empty;
            var state       = string.Empty;
            var postalCode  = string.Empty;

            // ── Extract unit designator (before splitting on comma so it doesn't confuse city) ──
            var unitMatch = UnitPattern.Match(working);
            if (unitMatch.Success)
            {
                unit = unitMatch.Value.Trim();
                working = working.Remove(unitMatch.Index, unitMatch.Length).Trim();
                working = NormalizeSpaces.Replace(working, " ");
            }

            // ── Split on commas to separate the street line from city/state/zip ──
            var parts = working.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // ── Parse state + ZIP from the last segment(s) ──
            // Look for "TX 75001" or "TX 75001-1234" pattern anywhere in the tail.
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                var stateZipMatch = StateZipPattern.Match(parts[i]);
                if (stateZipMatch.Success)
                {
                    var candidate = stateZipMatch.Groups[1].Value.ToUpperInvariant();
                    if (UsStateAbbreviations.Contains(candidate))
                    {
                        state      = candidate;
                        postalCode = stateZipMatch.Groups[2].Value;
                        // Remove this segment from parts so the city search ignores it.
                        parts[i] = StateZipPattern.Replace(parts[i], string.Empty).Trim();
                        break;
                    }
                }

                // ZIP alone (no preceding state abbreviation found)
                if (string.IsNullOrEmpty(postalCode))
                {
                    var zipOnly = ZipPattern.Match(parts[i]);
                    if (zipOnly.Success)
                    {
                        postalCode = zipOnly.Value;
                        parts[i] = parts[i].Replace(zipOnly.Value, string.Empty).Trim();
                    }
                }

                // Bare state abbreviation (e.g. "CO") when no ZIP is present in this segment.
                if (string.IsNullOrEmpty(state))
                {
                    var trimmed = parts[i].Trim();
                    if (trimmed.Length == 2 && UsStateAbbreviations.Contains(trimmed))
                    {
                        state = trimmed.ToUpperInvariant();
                        parts[i] = string.Empty;
                        break;
                    }
                }
            }

            // ── City: the segment immediately before the state/ZIP segment, or last non-empty part ──
            // The street is always parts[0]; city is the second-to-last meaningful segment.
            var nonEmptyParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            var streetSegment = nonEmptyParts.Count > 0 ? nonEmptyParts[0] : string.Empty;

            if (nonEmptyParts.Count >= 2)
            {
                // When there are 3+ segments: [street, city, state/zip-remnant]
                // When there are 2 segments: [street, city]
                city = ToTitleCase(nonEmptyParts[nonEmptyParts.Count == 2 ? 1 : nonEmptyParts.Count - 1]);
            }

            // ── Parse house number from street segment ──
            var houseMatch = HouseNumberPattern.Match(streetSegment);
            if (houseMatch.Success)
            {
                houseNumber   = houseMatch.Groups[1].Value;
                streetSegment = streetSegment[houseMatch.Length..].Trim();
            }

            // ── Expand street suffix abbreviation ──
            var streetTokens = streetSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (streetTokens.Length > 0)
            {
                var lastToken = streetTokens[^1];
                if (SuffixMap.TryGetValue(lastToken, out var expanded))
                {
                    streetSuffix = expanded;
                    streetName   = ToTitleCase(string.Join(" ", streetTokens[..^1]));
                }
                else
                {
                    streetName = ToTitleCase(streetSegment);
                }
            }

            // ── Compute parse confidence (how many of the 5 core components were extracted) ──
            var found = 0;
            if (!string.IsNullOrEmpty(houseNumber)) found++;
            if (!string.IsNullOrEmpty(streetName))  found++;
            if (!string.IsNullOrEmpty(city))         found++;
            if (!string.IsNullOrEmpty(state))        found++;
            if (!string.IsNullOrEmpty(postalCode))   found++;
            var parseConfidence = found / 5.0;

            // ── Build standardized single-line address ──
            var streetPart = string.Join(" ",
                new[] { houseNumber, streetName, streetSuffix }.Where(s => !string.IsNullOrEmpty(s)));

            var unitPart   = string.IsNullOrEmpty(unit) ? string.Empty : $" {unit}";
            var localePart = string.Join(", ",
                new[] { city, $"{state} {postalCode}".Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var standardized = string.IsNullOrWhiteSpace(localePart)
                ? $"{streetPart}{unitPart}".Trim()
                : $"{streetPart}{unitPart}, {localePart}".Trim();

            return new AddressParsedDto
            {
                HouseNumber         = houseNumber,
                StreetName          = streetName,
                StreetSuffix        = streetSuffix,
                Unit                = unit,
                City                = city,
                State               = state,
                PostalCode          = postalCode,
                StandardizedAddress = standardized,
                ParseConfidence     = parseConfidence
            };
        }

        // Determines the ConfidenceTier from a numeric ArcGIS score.
        private static ConfidenceTier DetermineConfidenceTier(double score)
        {
            if (score >= 90.0) return ConfidenceTier.High;
            if (score >= 75.0) return ConfidenceTier.Medium;
            if (score >= 50.0) return ConfidenceTier.Low;
            return ConfidenceTier.Unresolved;
        }

        // Builds a City+State+ZIP query string for the ArcGIS fallback.
        private static string BuildCityStateZipQuery(AddressParsedDto parsed)
        {
            return string.Join(", ",
                new[] { parsed.City, $"{parsed.State} {parsed.PostalCode}".Trim() }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        // Simple title-case helper (no external libraries).
        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Select(w =>
                char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
        }
    }
}
