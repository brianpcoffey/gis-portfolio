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

        // Street-suffix lookup (abbreviation OR full word) → canonical full word.
        // Covers the common USPS Publication 28 C1 suffixes so the parser reliably finds
        // the street/city boundary; full words map to themselves.
        private static readonly Dictionary<string, string> SuffixMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["St"] = "Street",       ["Street"] = "Street",
            ["Ave"] = "Avenue",      ["Av"] = "Avenue",        ["Avenue"] = "Avenue",
            ["Blvd"] = "Boulevard",  ["Blvd."] = "Boulevard",  ["Boulevard"] = "Boulevard",
            ["Dr"] = "Drive",        ["Drive"] = "Drive",
            ["Rd"] = "Road",         ["Road"] = "Road",
            ["Ln"] = "Lane",         ["Lane"] = "Lane",
            ["Ct"] = "Court",        ["Court"] = "Court",
            ["Pl"] = "Place",        ["Place"] = "Place",
            ["Hwy"] = "Highway",     ["Highway"] = "Highway",
            ["Way"] = "Way",
            ["Cir"] = "Circle",      ["Circle"] = "Circle",
            ["Pkwy"] = "Parkway",    ["Parkway"] = "Parkway",
            ["Ter"] = "Terrace",     ["Terr"] = "Terrace",     ["Terrace"] = "Terrace",
            ["Trl"] = "Trail",       ["Trail"] = "Trail",
            ["Loop"] = "Loop",
            ["Sq"] = "Square",       ["Square"] = "Square",
            ["Pike"] = "Pike",
            ["Row"] = "Row",
            ["Run"] = "Run",
            ["Pass"] = "Pass",
            ["Path"] = "Path",
            ["Plz"] = "Plaza",       ["Plaza"] = "Plaza",
            ["Cres"] = "Crescent",   ["Crescent"] = "Crescent",
            ["Aly"] = "Alley",       ["Alley"] = "Alley",
            ["Xing"] = "Crossing",   ["Crossing"] = "Crossing",
            ["Ctr"] = "Center",      ["Center"] = "Center",
            ["Cv"] = "Cove",         ["Cove"] = "Cove",
            ["Mnr"] = "Manor",       ["Manor"] = "Manor",
            ["Pt"] = "Point",        ["Point"] = "Point",
            ["Rdg"] = "Ridge",       ["Ridge"] = "Ridge",
            ["Vly"] = "Valley",      ["Valley"] = "Valley",
            ["Vw"] = "View",         ["View"] = "View",
            ["Grv"] = "Grove",       ["Grove"] = "Grove",
            ["Gdns"] = "Gardens",    ["Gardens"] = "Gardens",
            ["Blf"] = "Bluff",       ["Bluff"] = "Bluff"
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

        // Token-level patterns used during parsing.
        private static readonly Regex ZipTokenPattern    = new(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);
        private static readonly Regex NormalizeSpaces    = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex HouseNumberPattern = new(@"^(\d+)\s+", RegexOptions.Compiled);
        private static readonly Regex UnitPattern        = new(@"\b(apt|suite|ste|unit|#)\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StateZipPattern    = new(@"\b([A-Za-z]{2})\s+(\d{5}(?:-\d{4})?)\b", RegexOptions.Compiled);
        private static readonly Regex ZipPattern         = new(@"\b(\d{5}(?:-\d{4})?)\b", RegexOptions.Compiled);

        // Unit designator keywords (case-insensitive token match).
        private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Apt", "Suite", "Ste", "Unit", "#"
        };

        // Post-suffix directional qualifiers that are NOT city/state tokens.
        private static readonly HashSet<string> Directionals = new(StringComparer.OrdinalIgnoreCase)
        {
            "N", "S", "E", "W", "NW", "NE", "SW", "SE"
        };

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

        // Core parsing logic: token-based pipeline.
        // Commas are treated as whitespace; all parsing is driven by a single flat token array
        // so that comma-delimited and comma-free address formats are handled identically.
        private static AddressParsedDto ParseAddress(string rawAddress)
        {
            // Normalise: collapse whitespace, replace commas with spaces, then re-split into tokens.
            var normalised = NormalizeSpaces.Replace(rawAddress.Replace(',', ' ').Trim(), " ");
            var tokens = normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var houseNumber  = string.Empty;
            var streetName   = string.Empty;
            var streetSuffix = string.Empty;
            var directional  = string.Empty;
            var unit         = string.Empty;
            var city         = string.Empty;
            var state        = string.Empty;
            var postalCode   = string.Empty;

            var cursor = 0;
            var end    = tokens.Length - 1; // exclusive upper bound (shrinks as tail tokens are consumed)

            // ── Step 5 & 6: consume PostalCode and State from the tail first ──
            // This anchors the tail so everything in between is city/street.
            if (end >= 0 && ZipTokenPattern.IsMatch(tokens[end]))
            {
                postalCode = tokens[end];
                end--;
            }

            if (end >= 0 && tokens[end].Length == 2 && UsStateAbbreviations.Contains(tokens[end]))
            {
                state = tokens[end].ToUpperInvariant();
                end--;
            }

            // ── Step 2: HouseNumber ──
            // Matches a leading numeric token (digits only, or digits + single letter suffix like "123A").
            if (cursor <= end && tokens[cursor].Length > 0
                && char.IsDigit(tokens[cursor][0])
                && tokens[cursor].All(c => char.IsDigit(c) || char.IsLetter(c)))
            {
                houseNumber = tokens[cursor];
                cursor++;
            }

            // ── Step 3: StreetName + StreetSuffix ──
            // Walk tokens forward until we find one that is a known suffix abbreviation.
            var streetNameTokens = new List<string>();
            var suffixFound = false;

            while (cursor <= end)
            {
                var tok = tokens[cursor];
                if (SuffixMap.TryGetValue(tok, out var expandedSuffix))
                {
                    streetSuffix = expandedSuffix;
                    cursor++;
                    suffixFound = true;
                    break;
                }
                streetNameTokens.Add(tok);
                cursor++;
            }

            // Fallback: with no recognised suffix the forward walk consumed every remaining
            // token (including any city) into the street name. Treat the trailing token as the
            // city so "600 Broadway, Redlands" yields street "Broadway", city "Redlands".
            if (!suffixFound && streetNameTokens.Count >= 2)
            {
                city = ToTitleCase(streetNameTokens[^1]);
                streetNameTokens.RemoveAt(streetNameTokens.Count - 1);
            }

            streetName = ToTitleCase(string.Join(" ", streetNameTokens));

            // ── Step 8: Directional qualifier after suffix ──
            // A directional token immediately after the suffix (e.g. "NW" in "Ave NW") is stored
            // separately so it can be placed after the expanded suffix in the standardized address
            // ("Pennsylvania Avenue NW"), not before it. The state and ZIP have already been
            // consumed from the tail, so a directional that doubles as a state code (e.g. "NE")
            // here is unambiguously the directional and must NOT be excluded.
            if (suffixFound && cursor <= end)
            {
                var tok = tokens[cursor];
                if (Directionals.Contains(tok))
                {
                    directional = tok.ToUpperInvariant();
                    cursor++;
                }
            }

            // ── Step 4: Unit designator ──
            // Check whether the token at cursor is a unit keyword; if so, consume keyword + value.
            if (cursor <= end && UnitKeywords.Contains(tokens[cursor]))
            {
                var keyword = tokens[cursor];
                cursor++;
                if (cursor <= end)
                {
                    unit = $"{ToTitleCase(keyword)} {tokens[cursor].ToUpperInvariant()}";
                    cursor++;
                }
                else
                {
                    unit = ToTitleCase(keyword);
                }
            }

            // ── Step 7: City ──
            // All tokens remaining between cursor and end (inclusive) form the city.
            if (cursor <= end)
            {
                city = ToTitleCase(string.Join(" ", tokens[cursor..(end + 1)]));
            }

            // ── Step 9: ParseConfidence ──
            // Denominator is 6 when no unit is present (unit is optional and not penalised),
            // 7 when a unit was extracted (all 7 components are in play).
            var found = 0;
            if (!string.IsNullOrEmpty(houseNumber))  found++;
            if (!string.IsNullOrEmpty(streetName))   found++;
            if (!string.IsNullOrEmpty(streetSuffix)) found++;
            if (!string.IsNullOrEmpty(city))         found++;
            if (!string.IsNullOrEmpty(state))        found++;
            if (!string.IsNullOrEmpty(postalCode))   found++;

            double parseConfidence;
            if (!string.IsNullOrEmpty(unit))
            {
                found++;
                parseConfidence = found / 7.0;
            }
            else
            {
                parseConfidence = found / 6.0;
            }

            // ── Step 10: StandardizedAddress ──
            // directional goes after the expanded suffix: "Pennsylvania Avenue NW".
            var streetPart = string.Join(" ",
                new[] { houseNumber, streetName, streetSuffix, directional }.Where(s => !string.IsNullOrEmpty(s)));

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
