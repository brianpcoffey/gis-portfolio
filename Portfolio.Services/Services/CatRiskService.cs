using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class CatRiskService : ICatRiskService
    {
        private const int MaxLocations = 5_000;
        private const int MaxEvents = 20_000;
        private const long MaxEvaluations = 60_000_000;
        private const double MaxRadiusKm = 200.0;

        private const double EarthRadiusKm = 6371.0;
        private const double KmPerLatDegree = 111.32;

        // Return periods reported alongside the full curve. PML is conventionally the 250-year loss.
        private static readonly double[] BenchmarkReturnPeriods = [10, 25, 50, 100, 250, 500];
        private const double PmlReturnPeriod = 250;

        // The emitted curve is downsampled to a fixed number of log-spaced return periods:
        // a 20,000-event catalog would otherwise produce 20,000 points nobody can plot.
        private const int CurveSampleCount = 120;

        private readonly ILogger<CatRiskService> _logger;

        public CatRiskService(ILogger<CatRiskService> logger)
        {
            _logger = logger;
            CatRiskNativeBridge.LogAvailability(_logger);
        }

        public Task<PolicyBookDto> GetPolicyBookAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var locations = SoCalPolicyBook.Locations;
            var communities = locations
                .GroupBy(l => l.Community)
                .Select(g => new CommunityExposureDto
                {
                    Name = g.Key,
                    LocationCount = g.Count(),
                    TotalInsuredValue = g.Sum(l => l.InsuredValue),
                    MeanSiteHazard = Math.Round(g.Average(l => l.SiteHazard), 4)
                })
                .OrderByDescending(c => c.TotalInsuredValue)
                .ToList();

            return Task.FromResult(new PolicyBookDto
            {
                BookName = SoCalPolicyBook.BookName,
                Locations = [.. locations],
                Events = [.. SoCalPolicyBook.Events],
                TotalInsuredValue = locations.Sum(l => l.InsuredValue),
                LocationCount = locations.Count,
                Communities = communities
            });
        }

        // Computes TIV concentration within a ring around each location, preferring the
        // native kernel and falling back to managed code.
        public Task<AccumulationResultDto> ComputeAccumulationAsync(
            AccumulationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLocations(request.Locations, nameof(request));
            if (!IsFinite(request.RadiusKm) || request.RadiusKm <= 0 || request.RadiusKm > MaxRadiusKm)
                throw new ArgumentException($"Ring radius must be between 0 and {MaxRadiusKm} km.", nameof(request));
            if (!IsFinite(request.ConcentrationLimit) || request.ConcentrationLimit <= 0)
                throw new ArgumentException("Concentration limit must be greater than zero.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            bool nativeAccelerated;
            double[] ringTiv;
            int[] neighborCount;
            if (CatRiskNativeBridge.TryComputeRingAccumulation(request, _logger, out var nativeTiv, out var nativeNeighbors))
            {
                ringTiv = nativeTiv!;
                neighborCount = nativeNeighbors!;
                nativeAccelerated = true;
            }
            else
            {
                (ringTiv, neighborCount) = AccumulateManaged(request, cancellationToken);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildAccumulationResult(request, ringTiv, neighborCount, nativeAccelerated));
        }

        // Simulates the event catalog against the book, preferring the native kernel and
        // falling back to managed code. The kernel returns per-event losses; AAL and the
        // exceedance curve are derived here, where they stay readable and testable.
        public Task<SimulationResultDto> SimulateAsync(
            SimulationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLocations(request.Locations, nameof(request));
            ValidateEvents(request.Events, nameof(request));

            if ((long)request.Locations.Count * request.Events.Count > MaxEvaluations)
                throw new ArgumentException($"Simulation size is limited to {MaxEvaluations} site-event evaluations.", nameof(request));
            if (!IsFinite(request.VulnerabilityAlpha) || request.VulnerabilityAlpha <= 0)
                throw new ArgumentException("Vulnerability alpha must be greater than zero.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            bool nativeAccelerated;
            double[] eventLosses;
            int[] affectedCounts;
            if (CatRiskNativeBridge.TrySimulateEventLosses(request, _logger, out var nativeLosses, out var nativeAffected))
            {
                eventLosses = nativeLosses!;
                affectedCounts = nativeAffected!;
                nativeAccelerated = true;
            }
            else
            {
                (eventLosses, affectedCounts) = SimulateManaged(request, cancellationToken);
                nativeAccelerated = false;
            }

            return Task.FromResult(BuildSimulationResult(request, eventLosses, affectedCounts, nativeAccelerated));
        }

        // ── Validation ──────────────────────────────────────────────────────────

        private static void ValidateLocations(List<CatLocationDto> locations, string paramName)
        {
            if (locations is null || locations.Count == 0)
                throw new ArgumentException("At least one policy location is required.", paramName);
            if (locations.Count > MaxLocations)
                throw new ArgumentException($"Analyses are limited to {MaxLocations} policy locations.", paramName);

            foreach (var location in locations)
            {
                if (!IsFinite(location.Latitude) || !IsFinite(location.Longitude))
                    throw new ArgumentException("Location coordinates must be finite values.", paramName);
                if (!IsFinite(location.InsuredValue) || location.InsuredValue <= 0)
                    throw new ArgumentException("Insured value must be greater than zero.", paramName);
                if (!IsFinite(location.SiteHazard) || location.SiteHazard < 0 || location.SiteHazard > 1)
                    throw new ArgumentException("Site hazard must be between 0 and 1.", paramName);
                if (!IsFinite(location.DeductibleRate) || location.DeductibleRate < 0 || location.DeductibleRate > 1)
                    throw new ArgumentException("Deductible rate must be between 0 and 1.", paramName);
                if (!IsFinite(location.LimitRate) || location.LimitRate < 0 || location.LimitRate > 1)
                    throw new ArgumentException("Limit rate must be between 0 and 1.", paramName);
            }
        }

        private static void ValidateEvents(List<CatEventDto> events, string paramName)
        {
            if (events is null || events.Count == 0)
                throw new ArgumentException("At least one catastrophe event is required.", paramName);
            if (events.Count > MaxEvents)
                throw new ArgumentException($"Event catalogs are limited to {MaxEvents} events.", paramName);

            foreach (var catEvent in events)
            {
                if (!IsFinite(catEvent.Latitude) || !IsFinite(catEvent.Longitude))
                    throw new ArgumentException("Event coordinates must be finite values.", paramName);
                if (!IsFinite(catEvent.Intensity) || !IsFinite(catEvent.RadiusKm) || !IsFinite(catEvent.AnnualRate))
                    throw new ArgumentException("Event parameters must be finite values.", paramName);
                if (catEvent.RadiusKm <= 0)
                    throw new ArgumentException("Event radius must be greater than zero.", paramName);
                if (catEvent.AnnualRate < 0)
                    throw new ArgumentException("Event annual rate cannot be negative.", paramName);
            }
        }

        // ── Managed fallbacks (mirror the native kernel exactly) ────────────────

        private static (double[] RingTiv, int[] NeighborCount) AccumulateManaged(
            AccumulationRequestDto request,
            CancellationToken cancellationToken)
        {
            var locations = request.Locations;
            var ringTiv = new double[locations.Count];
            var neighborCount = new int[locations.Count];

            for (var i = 0; i < locations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var total = 0.0;
                var neighbors = 0;
                var lat = locations[i].Latitude;
                var lon = locations[i].Longitude;

                for (var j = 0; j < locations.Count; j++)
                {
                    var otherLat = locations[j].Latitude;
                    var otherLon = locations[j].Longitude;

                    if (i != j && OutsideBoundingBox(lat, lon, otherLat, otherLon, request.RadiusKm))
                        continue;

                    if (i == j || HaversineKm(lat, lon, otherLat, otherLon) <= request.RadiusKm)
                    {
                        total += locations[j].InsuredValue;
                        neighbors++;
                    }
                }

                ringTiv[i] = total;
                neighborCount[i] = neighbors;
            }

            return (ringTiv, neighborCount);
        }

        private static (double[] EventLosses, int[] AffectedCounts) SimulateManaged(
            SimulationRequestDto request,
            CancellationToken cancellationToken)
        {
            var locations = request.Locations;
            var events = request.Events;
            var eventLosses = new double[events.Count];
            var affectedCounts = new int[events.Count];

            for (var e = 0; e < events.Count; e++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var catEvent = events[e];
                var total = 0.0;
                var affected = 0;

                for (var l = 0; l < locations.Count; l++)
                {
                    var location = locations[l];

                    if (OutsideBoundingBox(catEvent.Latitude, catEvent.Longitude,
                        location.Latitude, location.Longitude, catEvent.RadiusKm))
                        continue;

                    var distanceKm = HaversineKm(catEvent.Latitude, catEvent.Longitude,
                        location.Latitude, location.Longitude);

                    var loss = GrossLoss(location, catEvent, request.VulnerabilityAlpha, distanceKm);
                    if (loss > 0)
                    {
                        total += loss;
                        affected++;
                    }
                }

                eventLosses[e] = total;
                affectedCounts[e] = affected;
            }

            return (eventLosses, affectedCounts);
        }

        // Gross loss for one location under one event, after deductible and limit.
        // Mirrors gross_loss() in native/cat_risk_kernel/src/cat_risk_kernel.cpp line for line.
        private static double GrossLoss(CatLocationDto location, CatEventDto catEvent, double alpha, double distanceKm)
        {
            if (catEvent.RadiusKm <= 0 || distanceKm >= catEvent.RadiusKm)
                return 0;

            var decay = 1.0 - (distanceKm / catEvent.RadiusKm);
            var siteIntensity = catEvent.Intensity * decay * location.SiteHazard;
            if (siteIntensity <= 0)
                return 0;

            var mdr = 1.0 - Math.Exp(-alpha * siteIntensity);
            var groundUp = location.InsuredValue * mdr;
            var retained = location.InsuredValue * location.DeductibleRate;
            var cap = location.InsuredValue * location.LimitRate;

            var net = groundUp - retained;
            if (net <= 0)
                return 0;
            return net > cap ? cap : net;
        }

        // ── Result shaping ──────────────────────────────────────────────────────

        private static AccumulationResultDto BuildAccumulationResult(
            AccumulationRequestDto request,
            double[] ringTiv,
            int[] neighborCount,
            bool nativeAccelerated)
        {
            var rings = new List<RingDto>(ringTiv.Length);
            var breachCount = 0;
            var worstRingTiv = 0.0;
            var worstLocationId = 0;

            for (var i = 0; i < ringTiv.Length; i++)
            {
                var breached = ringTiv[i] > request.ConcentrationLimit;
                if (breached)
                    breachCount++;

                if (ringTiv[i] > worstRingTiv)
                {
                    worstRingTiv = ringTiv[i];
                    worstLocationId = request.Locations[i].Id;
                }

                rings.Add(new RingDto
                {
                    LocationId = request.Locations[i].Id,
                    RingTiv = ringTiv[i],
                    NeighborCount = i < neighborCount.Length ? neighborCount[i] : 0,
                    Breached = breached
                });
            }

            return new AccumulationResultDto
            {
                NativeAccelerated = nativeAccelerated,
                RadiusKm = request.RadiusKm,
                ConcentrationLimit = request.ConcentrationLimit,
                Rings = rings,
                BreachCount = breachCount,
                WorstRingTiv = worstRingTiv,
                WorstLocationId = worstLocationId
            };
        }

        private static SimulationResultDto BuildSimulationResult(
            SimulationRequestDto request,
            double[] eventLosses,
            int[] affectedCounts,
            bool nativeAccelerated)
        {
            var events = request.Events;

            // Average Annual Loss is the rate-weighted mean of the event losses.
            var aal = 0.0;
            var totalRate = 0.0;
            var worstIndex = -1;
            for (var e = 0; e < eventLosses.Length; e++)
            {
                aal += events[e].AnnualRate * eventLosses[e];
                totalRate += events[e].AnnualRate;
                if (worstIndex < 0 || eventLosses[e] > eventLosses[worstIndex])
                    worstIndex = e;
            }

            var curve = BuildExceedanceCurve(events, eventLosses);

            var returnPeriodLosses = BenchmarkReturnPeriods
                .Select(rp => new ReturnPeriodLossDto { ReturnPeriod = rp, Loss = LossAtReturnPeriod(curve, rp) })
                .ToList();

            EventLossDto? worstEvent = null;
            if (worstIndex >= 0 && eventLosses[worstIndex] > 0)
            {
                worstEvent = new EventLossDto
                {
                    EventId = events[worstIndex].Id,
                    Latitude = events[worstIndex].Latitude,
                    Longitude = events[worstIndex].Longitude,
                    RadiusKm = events[worstIndex].RadiusKm,
                    Loss = eventLosses[worstIndex],
                    AffectedLocations = worstIndex < affectedCounts.Length ? affectedCounts[worstIndex] : 0
                };
            }

            return new SimulationResultDto
            {
                NativeAccelerated = nativeAccelerated,
                EventCount = events.Count,
                LocationCount = request.Locations.Count,
                AverageAnnualLoss = aal,
                ProbableMaximumLoss = LossAtReturnPeriod(curve, PmlReturnPeriod),
                ExceedanceCurve = DownsampleCurve(curve),
                ReturnPeriodLosses = returnPeriodLosses,
                WorstEvent = worstEvent,
                TotalAnnualRate = totalRate
            };
        }

        // Builds the occurrence exceedance probability curve.
        //
        // For a loss level L the annual exceedance rate is the summed frequency of every
        // event whose loss exceeds L, so sorting descending by loss and accumulating rate
        // yields one curve point per event: (returnPeriod = 1 / cumulativeRate, loss).
        // The result is ordered by ascending return period, which is also ascending loss.
        private static List<ExceedancePointDto> BuildExceedanceCurve(List<CatEventDto> events, double[] eventLosses)
        {
            var contributing = new List<(double Loss, double Rate)>();
            for (var e = 0; e < eventLosses.Length; e++)
            {
                if (eventLosses[e] > 0 && events[e].AnnualRate > 0)
                    contributing.Add((eventLosses[e], events[e].AnnualRate));
            }

            if (contributing.Count == 0)
                return [];

            contributing.Sort((a, b) => b.Loss.CompareTo(a.Loss));

            var points = new List<ExceedancePointDto>(contributing.Count);
            var cumulativeRate = 0.0;
            foreach (var (loss, rate) in contributing)
            {
                cumulativeRate += rate;
                points.Add(new ExceedancePointDto
                {
                    ReturnPeriod = 1.0 / cumulativeRate,
                    Loss = loss
                });
            }

            // Descending return period as built; reverse so the curve reads left to right.
            points.Reverse();
            return points;
        }

        // Log-linear interpolation of loss at a target return period. The curve is ordered
        // by ascending return period, so a plain forward scan brackets the target.
        private static double LossAtReturnPeriod(List<ExceedancePointDto> curve, double returnPeriod)
        {
            if (curve.Count == 0)
                return 0;
            if (returnPeriod <= curve[0].ReturnPeriod)
                return curve[0].Loss;
            if (returnPeriod >= curve[^1].ReturnPeriod)
                return curve[^1].Loss;

            for (var i = 0; i < curve.Count - 1; i++)
            {
                var lower = curve[i];
                var upper = curve[i + 1];
                if (returnPeriod < lower.ReturnPeriod || returnPeriod > upper.ReturnPeriod)
                    continue;

                var span = Math.Log(upper.ReturnPeriod) - Math.Log(lower.ReturnPeriod);
                if (span <= 0)
                    return upper.Loss;

                var t = (Math.Log(returnPeriod) - Math.Log(lower.ReturnPeriod)) / span;
                return lower.Loss + t * (upper.Loss - lower.Loss);
            }

            return curve[^1].Loss;
        }

        // Emits a fixed number of log-spaced samples so the payload stays plottable
        // regardless of catalog size.
        private static List<ExceedancePointDto> DownsampleCurve(List<ExceedancePointDto> curve)
        {
            if (curve.Count <= CurveSampleCount)
                return curve;

            var minRp = Math.Max(curve[0].ReturnPeriod, 1.0);
            var maxRp = curve[^1].ReturnPeriod;
            if (maxRp <= minRp)
                return curve;

            var logMin = Math.Log(minRp);
            var logMax = Math.Log(maxRp);
            var sampled = new List<ExceedancePointDto>(CurveSampleCount);

            for (var i = 0; i < CurveSampleCount; i++)
            {
                var t = (double)i / (CurveSampleCount - 1);
                var rp = Math.Exp(logMin + t * (logMax - logMin));
                sampled.Add(new ExceedancePointDto
                {
                    ReturnPeriod = rp,
                    Loss = LossAtReturnPeriod(curve, rp)
                });
            }

            return sampled;
        }

        // ── Geodesy (mirrors the native kernel) ─────────────────────────────────

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var sinLat = Math.Sin(dLat * 0.5);
            var sinLon = Math.Sin(dLon * 0.5);
            var a = sinLat * sinLat +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * sinLon * sinLon;
            return 2.0 * EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(a, 1.0)));
        }

        // Conservative bounding-box reject: true only when the pair is provably farther
        // apart than radiusKm. Never rejects a pair that is actually within the radius.
        private static bool OutsideBoundingBox(double lat1, double lon1, double lat2, double lon2, double radiusKm)
        {
            var dLat = Math.Abs(lat2 - lat1);
            if (dLat > radiusKm / KmPerLatDegree)
                return true;

            var maxAbsLat = Math.Max(Math.Abs(lat1), Math.Abs(lat2));
            var cosLat = Math.Cos(maxAbsLat * Math.PI / 180.0);
            if (cosLat < 1e-6)
                return false;

            var dLon = Math.Abs(lon2 - lon1);
            return dLon > radiusKm / (KmPerLatDegree * cosLat);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
