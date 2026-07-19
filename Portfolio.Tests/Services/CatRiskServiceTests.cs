using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.DTOs;
using Portfolio.Services.Data;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // Catastrophe risk analytics: ring accumulation (exposure concentration) and Monte
    // Carlo event-loss simulation producing AAL, PML, and the OEP curve.
    //
    // These tests assert computation, not which path produced it: they pass whether or
    // not the native shared libraries have been built. Native/managed equivalence is
    // covered by NativeParityTests and by `dotnet run --project Portfolio.Benchmarks`.
    // native/cat_risk_kernel/src/cat_risk_kernel.cpp line for line, so these also pin the
    // arithmetic the native kernel must reproduce.
    public class CatRiskServiceTests
    {
        // 0.01 degrees of latitude is ~1.112 km, which brackets the bounding-box reject
        // used by both the native and managed paths.
        private const double BaseLat = 34.0;
        private const double BaseLon = -117.0;
        private const double OneHundredthDegreeKm = 1.11195;

        private static CatRiskService NewService() =>
            new(NullLogger<CatRiskService>.Instance);

        // ── Ring accumulation ───────────────────────────────────────────────────

        [Fact]
        public async Task Accumulation_SingleLocation_RingEqualsOwnTiv()
        {
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations = [Location(1, BaseLat, BaseLon, insuredValue: 500_000)],
                RadiusKm = 5,
                ConcentrationLimit = 50_000_000
            });

            var ring = Assert.Single(result.Rings);
            Assert.Equal(500_000, ring.RingTiv);
            Assert.Equal(1, ring.NeighborCount);
            Assert.False(ring.Breached);
        }

        [Fact]
        public async Task Accumulation_TwoLocationsWithinRadius_EachRingIncludesBoth()
        {
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations =
                [
                    Location(1, BaseLat, BaseLon, insuredValue: 400_000),
                    Location(2, BaseLat + 0.01, BaseLon, insuredValue: 600_000)
                ],
                RadiusKm = 5,
                ConcentrationLimit = 50_000_000
            });

            Assert.All(result.Rings, r =>
            {
                Assert.Equal(1_000_000, r.RingTiv);
                Assert.Equal(2, r.NeighborCount);
            });
        }

        [Fact]
        public async Task Accumulation_TwoLocationsOutsideRadius_RingsAreIndependent()
        {
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations =
                [
                    Location(1, BaseLat, BaseLon, insuredValue: 400_000),
                    Location(2, BaseLat + 0.01, BaseLon, insuredValue: 600_000)
                ],
                // 1.0 km is inside the ~1.112 km separation, so neither sees the other.
                RadiusKm = 1.0,
                ConcentrationLimit = 50_000_000
            });

            Assert.Equal(400_000, result.Rings[0].RingTiv);
            Assert.Equal(600_000, result.Rings[1].RingTiv);
            Assert.All(result.Rings, r => Assert.Equal(1, r.NeighborCount));
        }

        [Fact]
        public async Task Accumulation_NeighborJustInsideRadius_IsCounted()
        {
            // Regression guard: the bounding-box pre-filter must never reject a pair that
            // is genuinely within the radius.
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations =
                [
                    Location(1, BaseLat, BaseLon, insuredValue: 100_000),
                    Location(2, BaseLat + 0.01, BaseLon, insuredValue: 100_000)
                ],
                RadiusKm = OneHundredthDegreeKm + 0.05,
                ConcentrationLimit = 50_000_000
            });

            Assert.All(result.Rings, r => Assert.Equal(2, r.NeighborCount));
        }

        [Fact]
        public async Task Accumulation_ExceedsConcentrationLimit_FlagsBreach()
        {
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations =
                [
                    Location(1, BaseLat, BaseLon, insuredValue: 30_000_000),
                    Location(2, BaseLat + 0.01, BaseLon, insuredValue: 30_000_000)
                ],
                RadiusKm = 5,
                ConcentrationLimit = 50_000_000
            });

            Assert.Equal(2, result.BreachCount);
            Assert.All(result.Rings, r => Assert.True(r.Breached));
            Assert.Equal(60_000_000, result.WorstRingTiv);
        }

        [Fact]
        public async Task Accumulation_WorstLocationIdMatchesLargestRing()
        {
            var service = NewService();

            var result = await service.ComputeAccumulationAsync(new AccumulationRequestDto
            {
                Locations =
                [
                    Location(7, BaseLat, BaseLon, insuredValue: 100_000),
                    Location(8, BaseLat + 5.0, BaseLon, insuredValue: 900_000)
                ],
                RadiusKm = 1,
                ConcentrationLimit = 50_000_000
            });

            Assert.Equal(8, result.WorstLocationId);
            Assert.Equal(900_000, result.WorstRingTiv);
        }

        [Fact]
        public async Task Accumulation_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ComputeAccumulationAsync(null!));
        }

        [Fact]
        public async Task Accumulation_NoLocations_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeAccumulationAsync(new AccumulationRequestDto { Locations = [], RadiusKm = 5 }));
        }

        [Fact]
        public async Task Accumulation_ZeroRadius_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeAccumulationAsync(new AccumulationRequestDto
                {
                    Locations = [Location(1, BaseLat, BaseLon)],
                    RadiusKm = 0
                }));
        }

        [Fact]
        public async Task Accumulation_SiteHazardOutOfRange_ThrowsArgumentException()
        {
            var service = NewService();
            var bad = Location(1, BaseLat, BaseLon);
            bad.SiteHazard = 1.5;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeAccumulationAsync(new AccumulationRequestDto { Locations = [bad], RadiusKm = 5 }));
        }

        [Fact]
        public async Task Accumulation_NonPositiveInsuredValue_ThrowsArgumentException()
        {
            var service = NewService();
            var bad = Location(1, BaseLat, BaseLon);
            bad.InsuredValue = 0;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ComputeAccumulationAsync(new AccumulationRequestDto { Locations = [bad], RadiusKm = 5 }));
        }

        // ── Loss simulation ─────────────────────────────────────────────────────

        [Fact]
        public async Task Simulate_DirectHit_ProducesExactModelledLoss()
        {
            // Fully exposed site, no deductible, no limit binding, epicenter on the
            // location: mdr = 1 - exp(-alpha) with alpha = 3.
            var service = NewService();
            var location = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            location.SiteHazard = 1.0;
            location.DeductibleRate = 0.0;
            location.LimitRate = 1.0;

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [location],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            var expectedLoss = (1.0 - Math.Exp(-3.0)) * 1_000_000;

            Assert.NotNull(result.WorstEvent);
            Assert.Equal(expectedLoss, result.WorstEvent!.Loss, 3);
            Assert.Equal(1, result.WorstEvent.AffectedLocations);
            Assert.Equal(0.01 * expectedLoss, result.AverageAnnualLoss, 4);
        }

        [Fact]
        public async Task Simulate_EventFarFromBook_ProducesZeroLoss()
        {
            var service = NewService();

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [Location(1, BaseLat, BaseLon)],
                Events = [Event(1, 40.0, -120.0, intensity: 1.0, radiusKm: 5, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            Assert.Equal(0, result.AverageAnnualLoss);
            Assert.Equal(0, result.ProbableMaximumLoss);
            Assert.Null(result.WorstEvent);
            Assert.Empty(result.ExceedanceCurve);
        }

        [Fact]
        public async Task Simulate_LimitCapsTheLoss()
        {
            var service = NewService();
            var location = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            location.SiteHazard = 1.0;
            location.DeductibleRate = 0.0;
            location.LimitRate = 0.5;

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [location],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            Assert.NotNull(result.WorstEvent);
            Assert.Equal(500_000, result.WorstEvent!.Loss, 3);
        }

        [Fact]
        public async Task Simulate_DeductibleExceedsDamage_ProducesZeroLoss()
        {
            var service = NewService();
            var location = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            location.SiteHazard = 1.0;
            location.DeductibleRate = 0.99; // retains more than the modelled damage
            location.LimitRate = 1.0;

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [location],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            Assert.Equal(0, result.AverageAnnualLoss);
            Assert.Null(result.WorstEvent);
        }

        [Fact]
        public async Task Simulate_SiteHazardZero_ProducesZeroLoss()
        {
            var service = NewService();
            var location = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            location.SiteHazard = 0.0;

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [location],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            Assert.Equal(0, result.AverageAnnualLoss);
            Assert.Null(result.WorstEvent);
        }

        [Fact]
        public async Task Simulate_HigherVulnerabilityAlpha_IncreasesLoss()
        {
            var service = NewService();
            var request = new SimulationRequestDto
            {
                Locations = [Location(1, BaseLat, BaseLon, insuredValue: 1_000_000)],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.01)],
                VulnerabilityAlpha = 1.0
            };

            var low = await service.SimulateAsync(request);
            request.VulnerabilityAlpha = 6.0;
            var high = await service.SimulateAsync(request);

            Assert.True(high.AverageAnnualLoss > low.AverageAnnualLoss);
        }

        [Fact]
        public async Task Simulate_LossDecaysWithDistanceFromEpicenter()
        {
            var service = NewService();
            var near = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            var far = Location(2, BaseLat + 0.05, BaseLon, insuredValue: 1_000_000);

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [near, far],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 20, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            // Both are inside the footprint, so the event touches two locations, and the
            // total is strictly less than twice the on-epicenter loss because of decay.
            Assert.NotNull(result.WorstEvent);
            Assert.Equal(2, result.WorstEvent!.AffectedLocations);

            var onEpicenter = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [near],
                Events = [Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 20, annualRate: 0.01)],
                VulnerabilityAlpha = 3.0
            });

            Assert.True(result.WorstEvent.Loss < onEpicenter.WorstEvent!.Loss * 2);
        }

        [Fact]
        public async Task Simulate_AverageAnnualLoss_EqualsRateWeightedSum()
        {
            var service = NewService();
            var location = Location(1, BaseLat, BaseLon, insuredValue: 1_000_000);
            location.SiteHazard = 1.0;
            location.DeductibleRate = 0.0;
            location.LimitRate = 1.0;

            // One event hits, one misses entirely, so AAL must equal rate x hit loss.
            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = [location],
                Events =
                [
                    Event(1, BaseLat, BaseLon, intensity: 1.0, radiusKm: 10, annualRate: 0.02),
                    Event(2, 40.0, -120.0, intensity: 1.0, radiusKm: 5, annualRate: 0.50)
                ],
                VulnerabilityAlpha = 3.0
            });

            var expectedLoss = (1.0 - Math.Exp(-3.0)) * 1_000_000;
            Assert.Equal(0.02 * expectedLoss, result.AverageAnnualLoss, 4);
        }

        [Fact]
        public async Task Simulate_ExceedanceCurve_IsMonotonicallyIncreasingWithReturnPeriod()
        {
            var service = NewService();
            var result = await service.SimulateAsync(SpreadScenario());

            Assert.NotEmpty(result.ExceedanceCurve);
            for (var i = 1; i < result.ExceedanceCurve.Count; i++)
            {
                Assert.True(result.ExceedanceCurve[i].ReturnPeriod >= result.ExceedanceCurve[i - 1].ReturnPeriod);
                Assert.True(result.ExceedanceCurve[i].Loss >= result.ExceedanceCurve[i - 1].Loss - 1e-6);
            }
        }

        [Fact]
        public async Task Simulate_PmlEqualsOepAt250()
        {
            var service = NewService();
            var result = await service.SimulateAsync(SpreadScenario());

            var rp250 = result.ReturnPeriodLosses.Single(r => r.ReturnPeriod == 250);
            Assert.Equal(rp250.Loss, result.ProbableMaximumLoss, 6);
        }

        [Fact]
        public async Task Simulate_ReportsBenchmarkReturnPeriods()
        {
            var service = NewService();
            var result = await service.SimulateAsync(SpreadScenario());

            Assert.Equal([10d, 25d, 50d, 100d, 250d, 500d],
                result.ReturnPeriodLosses.Select(r => r.ReturnPeriod).ToArray());
        }

        [Fact]
        public async Task Simulate_TotalAnnualRate_SumsCatalogFrequencies()
        {
            var service = NewService();
            var result = await service.SimulateAsync(SpreadScenario());

            Assert.Equal(SpreadScenario().Events.Sum(e => e.AnnualRate), result.TotalAnnualRate, 8);
        }

        [Fact]
        public async Task Simulate_NullRequest_ThrowsArgumentNullException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SimulateAsync(null!));
        }

        [Fact]
        public async Task Simulate_NoEvents_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SimulateAsync(new SimulationRequestDto
                {
                    Locations = [Location(1, BaseLat, BaseLon)],
                    Events = []
                }));
        }

        [Fact]
        public async Task Simulate_NonPositiveAlpha_ThrowsArgumentException()
        {
            var service = NewService();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SimulateAsync(new SimulationRequestDto
                {
                    Locations = [Location(1, BaseLat, BaseLon)],
                    Events = [Event(1, BaseLat, BaseLon)],
                    VulnerabilityAlpha = 0
                }));
        }

        [Fact]
        public async Task Simulate_NonPositiveEventRadius_ThrowsArgumentException()
        {
            var service = NewService();
            var bad = Event(1, BaseLat, BaseLon);
            bad.RadiusKm = 0;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SimulateAsync(new SimulationRequestDto
                {
                    Locations = [Location(1, BaseLat, BaseLon)],
                    Events = [bad]
                }));
        }

        [Fact]
        public async Task Simulate_ExceedsEvaluationLimit_ThrowsArgumentException()
        {
            var service = NewService();
            // 5,000 x 13,000 = 65M evaluations, above the 60M ceiling.
            var locations = Enumerable.Range(1, 5_000)
                .Select(i => Location(i, BaseLat, BaseLon))
                .ToList();
            var events = Enumerable.Range(1, 13_000)
                .Select(i => Event(i, BaseLat, BaseLon))
                .ToList();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SimulateAsync(new SimulationRequestDto { Locations = locations, Events = events }));
        }

        // ── Policy book ─────────────────────────────────────────────────────────

        [Fact]
        public async Task PolicyBook_IsDeterministic()
        {
            var service = NewService();

            var first = await service.GetPolicyBookAsync();
            var second = await service.GetPolicyBookAsync();

            Assert.Equal(first.LocationCount, second.LocationCount);
            Assert.Equal(first.TotalInsuredValue, second.TotalInsuredValue);
            for (var i = 0; i < first.Locations.Count; i++)
            {
                Assert.Equal(first.Locations[i].Latitude, second.Locations[i].Latitude);
                Assert.Equal(first.Locations[i].Longitude, second.Locations[i].Longitude);
                Assert.Equal(first.Locations[i].InsuredValue, second.Locations[i].InsuredValue);
                Assert.Equal(first.Locations[i].SiteHazard, second.Locations[i].SiteHazard);
            }
        }

        [Fact]
        public async Task PolicyBook_MaterializesOnce()
        {
            var service = NewService();
            await service.GetPolicyBookAsync();

            Assert.Same(SoCalPolicyBook.Locations, SoCalPolicyBook.Locations);
            Assert.Same(SoCalPolicyBook.Events, SoCalPolicyBook.Events);
        }

        [Fact]
        public async Task PolicyBook_HasPlausibleValues()
        {
            var service = NewService();
            var book = await service.GetPolicyBookAsync();

            Assert.True(book.LocationCount >= 800);
            Assert.True(book.TotalInsuredValue > 0);
            Assert.Equal(6, book.Communities.Count);

            Assert.All(book.Locations, l =>
            {
                // San Bernardino / Riverside foothills bounding box.
                Assert.InRange(l.Latitude, 33.90, 34.20);
                Assert.InRange(l.Longitude, -117.40, -116.80);
                Assert.True(l.InsuredValue > 0);
                Assert.InRange(l.SiteHazard, 0.0, 1.0);
                Assert.InRange(l.DeductibleRate, 0.0, 1.0);
                Assert.InRange(l.LimitRate, 0.0, 1.0);
                Assert.False(string.IsNullOrWhiteSpace(l.Name));
                Assert.False(string.IsNullOrWhiteSpace(l.Community));
            });
        }

        [Fact]
        public async Task PolicyBook_EventCatalogCarriesTargetFrequency()
        {
            var service = NewService();
            var book = await service.GetPolicyBookAsync();

            Assert.NotEmpty(book.Events);
            Assert.All(book.Events, e =>
            {
                Assert.True(e.RadiusKm > 0);
                Assert.InRange(e.Intensity, 0.0, 1.0);
                Assert.True(e.AnnualRate > 0);
            });

            // The catalog is normalized to ~1.5 events per year across the region.
            Assert.Equal(1.5, book.Events.Sum(e => e.AnnualRate), 6);
        }

        [Fact]
        public async Task PolicyBook_CommunityRollupsMatchLocations()
        {
            var service = NewService();
            var book = await service.GetPolicyBookAsync();

            Assert.Equal(book.LocationCount, book.Communities.Sum(c => c.LocationCount));
            Assert.Equal(book.TotalInsuredValue, book.Communities.Sum(c => c.TotalInsuredValue), 2);
        }

        [Fact]
        public async Task PolicyBook_EndToEndSimulationProducesPositiveLoss()
        {
            // Guards the shipped dataset: the book and catalog must actually interact.
            var service = NewService();
            var book = await service.GetPolicyBookAsync();

            var result = await service.SimulateAsync(new SimulationRequestDto
            {
                Locations = book.Locations,
                Events = book.Events,
                VulnerabilityAlpha = 3.0
            });

            Assert.True(result.AverageAnnualLoss > 0);
            Assert.True(result.ProbableMaximumLoss > 0);
            Assert.NotEmpty(result.ExceedanceCurve);
            Assert.NotNull(result.WorstEvent);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static CatLocationDto Location(int id, double latitude, double longitude, double insuredValue = 750_000) =>
            new()
            {
                Id = id,
                Name = $"Location {id}",
                Community = "Test",
                Latitude = latitude,
                Longitude = longitude,
                InsuredValue = insuredValue,
                SiteHazard = 0.6,
                DeductibleRate = 0.03,
                LimitRate = 0.9
            };

        private static CatEventDto Event(
            int id,
            double latitude,
            double longitude,
            double intensity = 0.8,
            double radiusKm = 10,
            double annualRate = 0.01) =>
            new()
            {
                Id = id,
                Latitude = latitude,
                Longitude = longitude,
                Intensity = intensity,
                RadiusKm = radiusKm,
                AnnualRate = annualRate
            };

        // A catalog with a spread of losses and frequencies, so the exceedance curve has
        // real structure to assert against.
        private static SimulationRequestDto SpreadScenario()
        {
            var locations = new List<CatLocationDto>();
            for (var i = 0; i < 20; i++)
                locations.Add(Location(i + 1, BaseLat + i * 0.004, BaseLon + i * 0.004, insuredValue: 500_000 + i * 25_000));

            var events = new List<CatEventDto>();
            for (var i = 0; i < 40; i++)
            {
                events.Add(Event(
                    i + 1,
                    BaseLat + (i % 8) * 0.008,
                    BaseLon + (i % 5) * 0.008,
                    intensity: 0.35 + (i % 7) * 0.09,
                    radiusKm: 3 + (i % 6) * 4,
                    annualRate: 0.004 + (i % 9) * 0.0015));
            }

            return new SimulationRequestDto
            {
                Locations = locations,
                Events = events,
                VulnerabilityAlpha = 3.0
            };
        }
    }
}
