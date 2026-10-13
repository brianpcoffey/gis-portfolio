using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Native;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class HomeScoringServiceTests
    {
        private readonly Mock<IPropertyRepository> _propertyRepoMock;
        private readonly Mock<ILogger<HomeScoringService>> _loggerMock;
        private readonly HomeScoringService _service;

        public HomeScoringServiceTests()
        {
            _propertyRepoMock = new Mock<IPropertyRepository>();
            _loggerMock = new Mock<ILogger<HomeScoringService>>();
            _service = new HomeScoringService(_propertyRepoMock.Object, _loggerMock.Object, TimeProvider.System);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_ReturnsScoredProperties()
        {
            var prefs = new HomeSearchPreferencesDto { MaxPrice = 1000000, MinBedrooms = 2, MinBathrooms = 2, MinSqft = 1000, MaxCommuteMin = 60 };
            _propertyRepoMock.Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property> { new Property { Id = 1 }, new Property { Id = 2 } });
            var result = await _service.GetTopPropertiesAsync(prefs);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_WhenNoProperties_ReturnsEmptyList()
        {
            var prefs = new HomeSearchPreferencesDto { MaxPrice = 1000000, MinBedrooms = 2, MinBathrooms = 2, MinSqft = 1000, MaxCommuteMin = 60 };
            _propertyRepoMock.Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property>());
            var result = await _service.GetTopPropertiesAsync(prefs);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetPropertyByIdAsync_WhenFound_ReturnsScoredPropertyDto()
        {
            // Arrange
            var property = new Property { Id = 42, Street = "123 Main St", City = "Denver", ZipCode = "80203" };
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(property);

            // Act
            var result = await _service.GetPropertyByIdAsync(42);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.PropertyId);
            Assert.Equal("123 Main St", result.Street);
            Assert.Equal("Denver", result.City);
        }

        [Fact]
        public async Task GetPropertyByIdAsync_WhenNotFound_ReturnsNull()
        {
            // Arrange
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Property?)null);

            // Act
            var result = await _service.GetPropertyByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetPropertyByIdAsync_PassesCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var token = cts.Token;
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(1, token))
                .ReturnsAsync((Property?)null);

            // Act
            await _service.GetPropertyByIdAsync(1, token);

            // Assert
            _propertyRepoMock.Verify(r => r.GetByIdAsync(1, token), Times.Once);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_RanksAreAssignedSequentially()
        {
            // Arrange
            var prefs = MakePrefs();
            var properties = new List<Property>
            {
                MakeProperty(id: 1, price: 400_000m, commute: 10),   // best commute / affordable
                MakeProperty(id: 2, price: 750_000m, commute: 44),
                MakeProperty(id: 3, price: 550_000m, commute: 25)
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 3);

            // Assert – ranks must be 1, 2, 3 in result order (already sorted desc)
            Assert.Equal(1, result[0].Rank);
            Assert.Equal(2, result[1].Rank);
            Assert.Equal(3, result[2].Rank);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_TopNTruncatesResults()
        {
            // Arrange
            var prefs = MakePrefs();
            var properties = Enumerable.Range(1, 20)
                .Select(i => MakeProperty(id: i, price: 400_000m + i * 1_000m))
                .ToList();
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 5);

            // Assert
            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_ResultsOrderedByCompositeScoreDescending()
        {
            // Arrange – property 1 is clearly cheaper and has a shorter commute, so it scores higher
            var prefs = MakePrefs();
            var properties = new List<Property>
            {
                MakeProperty(id: 1, price: 350_000m, commute: 5),
                MakeProperty(id: 2, price: 790_000m, commute: 44)
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 2);

            // Assert – first result must have a composite score >= second result
            Assert.True(result[0].CompositeScore >= result[1].CompositeScore);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_CompositeScoreIsClamped0To100()
        {
            // Arrange – extreme inputs that could overflow scoring formulas
            var prefs = MakePrefs();
            var properties = new List<Property>
            {
                // Perfect property: very low price, very short commute, perfect condition scores
                MakeProperty(id: 1, price: 1m, commute: 0,
                    schoolRating: 100, crimeScore: 0, walkability: 100, transitAccess: 100,
                    roof: 100, ac: 100, plumbing: 100, electrical: 100,
                    floodRisk: 0, noiseLevel: 0)
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 1);

            // Assert
            Assert.InRange(result[0].CompositeScore, 0.0, 100.0);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_SubScoresAreAllClamped0To100()
        {
            // Arrange
            var prefs = MakePrefs();
            var properties = new List<Property>
            {
                MakeProperty(id: 1, price: 400_000m, commute: 20)
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 1);
            var dto = result[0];

            // Assert
            Assert.InRange(dto.AffordabilityScore, 0, 100);
            Assert.InRange(dto.NeighborhoodScore, 0, 100);
            Assert.InRange(dto.SizeScore, 0, 100);
            Assert.InRange(dto.AppreciationScore, 0, 100);
            Assert.InRange(dto.ConditionScore, 0, 100);
            Assert.InRange(dto.CommuteScore, 0, 100);
            Assert.InRange(dto.AmenitiesScore, 0, 100);
            Assert.InRange(dto.TaxUtilitiesScore, 0, 100);
            Assert.InRange(dto.ResaleScore, 0, 100);
            Assert.InRange(dto.EnvironmentScore, 0, 100);
            Assert.InRange(dto.CompositeScore, 0, 100);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_PassesCancellationTokenToRepository()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var token = cts.Token;
            var prefs = MakePrefs();
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), token))
                .ReturnsAsync(new List<Property>());

            // Act
            await _service.GetTopPropertiesAsync(prefs, cancellationToken: token);

            // Assert
            _propertyRepoMock.Verify(r => r.GetFilteredAsync(
                It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<int>(), token), Times.Once);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_EstimatedMonthlyCostIsPositive()
        {
            // Arrange
            var prefs = MakePrefs();
            var properties = new List<Property> { MakeProperty(id: 1, price: 500_000m) };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 1);

            // Assert – 80% of $500k at 6.5% / 30yr must yield a positive monthly payment
            Assert.True(result[0].EstimatedMonthlyCost > 0);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_AllWeightsZero_StillReturnsResults()
        {
            // Arrange – all weights zero; totalWeight guard must prevent divide-by-zero
            var prefs = new HomeSearchPreferencesDto
            {
                MaxPrice = 1_000_000m,
                MaxMonthlyBudget = 5_000m,
                MinBedrooms = 2,
                MinBathrooms = 1,
                MinSqft = 800,
                MaxCommuteMin = 60,
                WeightAffordability = 0,
                WeightNeighborhood = 0,
                WeightSize = 0,
                WeightAppreciation = 0,
                WeightCondition = 0,
                WeightCommute = 0,
                WeightAmenities = 0,
                WeightTaxUtilities = 0,
                WeightResale = 0,
                WeightEnvironment = 0
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property> { MakeProperty(id: 1, price: 400_000m) });

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 1);

            // Assert – must not throw; composite should still be in range
            Assert.Single(result);
            Assert.InRange(result[0].CompositeScore, 0.0, 100.0);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_DtoFieldsMappedFromEntity()
        {
            // Arrange
            var prefs = MakePrefs();
            var property = new Property
            {
                Id = 99,
                Street = "456 Oak Ave",
                City = "Redlands",
                ZipCode = "92373",
                Price = 500_000m,
                Bedrooms = 3,
                Bathrooms = 2,
                LotSqft = 1_800,
                Latitude = 34.05,
                Longitude = -117.18,
                PropertyTax = 6_000m,
                HoaFee = 100m,
                Utilities = 120m,
                SchoolRating = 75,
                CrimeScore = 25,
                Walkability = 60,
                TransitAccess = 50,
                AmenitiesScore = 65,
                FutureAppreciation = 60,
                ResalePotential = 55,
                RoofCondition = 80,
                AcCondition = 75,
                PlumbingCondition = 85,
                ElectricalCondition = 82,
                FloodRisk = 15,
                NoiseLevel = 25,
                CommuteMin = 20
            };
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property> { property });

            // Act
            var result = await _service.GetTopPropertiesAsync(prefs, top: 1);
            var dto = result[0];

            // Assert – scalar entity fields round-trip through the DTO
            Assert.Equal(99, dto.PropertyId);
            Assert.Equal("456 Oak Ave", dto.Street);
            Assert.Equal("Redlands", dto.City);
            Assert.Equal("92373", dto.ZipCode);
            Assert.Equal(500_000m, dto.Price);
            Assert.Equal(3, dto.Bedrooms);
            Assert.Equal(2, dto.Bathrooms);
            Assert.Equal(1_800, dto.LotSqft);
            Assert.Equal(34.05, dto.Latitude);
            Assert.Equal(-117.18, dto.Longitude);
        }

        // ---------------------------------------------------------------------------------------
        // Native batch call and its managed fallback. These go through the internal constructor, which
        // stands in for the native library, so they run without it and on every platform.
        // ---------------------------------------------------------------------------------------

        // The kinds NativeKernelLoader.IsNativeInvocationException classifies as a native failure
        // (the SEHException case is covered by NativeKernelLoaderTests).
        private static Exception NativeFailure(string kind) => kind switch
        {
            "status" => new InvalidOperationException("Native scoring kernel failed with status -99 (native exception)."),
            "missing library" => new DllNotFoundException("portfolio_scoring"),
            "missing entry point" => new EntryPointNotFoundException("ScorePropertyBatch"),
            "wrong architecture" => new BadImageFormatException("wrong architecture"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        [Theory]
        [InlineData("status")]
        [InlineData("missing library")]
        [InlineData("missing entry point")]
        [InlineData("wrong architecture")]
        public async Task GetTopPropertiesAsync_WhenTheNativeBatchFails_FallsBackToIdenticalManagedResults(string kind)
        {
            var failure = NativeFailure(kind);
            var prefs = MakePrefs();
            var properties = SomeProperties();
            SetupRepository(properties);
            var logger = new Repositories.CapturingLogger<HomeScoringService>();
            var nativeCalls = 0;
            var failing = new HomeScoringService(_propertyRepoMock.Object, logger, TimeProvider.System, true,
                (_, _) => { nativeCalls++; throw failure; });
            var managed = ManagedOnlyService();

            var result = await failing.GetTopPropertiesAsync(prefs, top: 4);
            var expected = await managed.GetTopPropertiesAsync(prefs, top: 4);

            Assert.Equal(1, nativeCalls);
            Assert.Equal(4, result.Count);
            Assert.Equal(Serialize(expected), Serialize(result));
            var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.Contains("falling back to managed scoring", warning.Message);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_EveryFailedNativeCallLogsAndFallsBack()
        {
            var prefs = MakePrefs();
            SetupRepository(SomeProperties());
            var logger = new Repositories.CapturingLogger<HomeScoringService>();
            var service = new HomeScoringService(_propertyRepoMock.Object, logger, TimeProvider.System, true,
                (_, _) => throw new InvalidOperationException("status -3"));

            var first = await service.GetTopPropertiesAsync(prefs, top: 3);
            var second = await service.GetTopPropertiesAsync(prefs, top: 3);

            Assert.Equal(Serialize(first), Serialize(second));
            Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Warning));
        }

        [Fact]
        public async Task GetTopPropertiesAsync_WhenTheNativeFailureIsNotANativeOne_DoesNotFallBack()
        {
            SetupRepository(SomeProperties());
            var service = new HomeScoringService(_propertyRepoMock.Object, _loggerMock.Object, TimeProvider.System, true,
                (_, _) => throw new ArgumentException("a bug in the caller, not a native failure"));

            await Assert.ThrowsAsync<ArgumentException>(() => service.GetTopPropertiesAsync(MakePrefs()));
        }

        [Fact]
        public async Task GetTopPropertiesAsync_WhenNativeIsUnavailable_NeverCallsTheNativeBatch()
        {
            SetupRepository(SomeProperties());
            var service = ManagedOnlyService();

            var result = await service.GetTopPropertiesAsync(MakePrefs(), top: 2);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_WhenTheNativeBatchSucceeds_UsesItsScoresRankedByComposite()
        {
            var properties = new List<Property>
            {
                MakeProperty(id: 1), MakeProperty(id: 2), MakeProperty(id: 3)
            };
            SetupRepository(properties);
            var composites = new[] { 41.126, 88.004, 63.5 };
            var logger = new Repositories.CapturingLogger<HomeScoringService>();
            var service = new HomeScoringService(_propertyRepoMock.Object, logger, TimeProvider.System, true,
                (batch, _) => batch.Select((_, i) => new ScoreOutputNative
                {
                    Affordability = 10 + i,
                    Neighborhood = 20 + i,
                    Size = 30 + i,
                    Appreciation = 40 + i,
                    Condition = 50 + i,
                    Commute = 60 + i,
                    Amenities = 70 + i,
                    TaxUtilities = 80 + i,
                    Resale = 90 + i,
                    Environment = 95 + i,
                    Composite = composites[i],
                    EstimatedMonthlyCost = 2_345.678 + i
                }).ToArray());

            var result = await service.GetTopPropertiesAsync(MakePrefs(), top: 2);

            Assert.Equal(new[] { 2, 3 }, result.Select(r => r.PropertyId));
            Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Rank));
            Assert.Equal(new[] { 88.0, 63.5 }, result.Select(r => r.CompositeScore));
            Assert.Equal(2_346.68m, result[0].EstimatedMonthlyCost);
            Assert.Equal(11, result[0].AffordabilityScore);
            Assert.Equal(96, result[0].EnvironmentScore);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        private HomeScoringService ManagedOnlyService() =>
            new(_propertyRepoMock.Object, new Repositories.CapturingLogger<HomeScoringService>(), TimeProvider.System, false,
                (_, _) => throw new InvalidOperationException("the native batch must not be called when native is unavailable"));

        private void SetupRepository(List<Property> properties) =>
            _propertyRepoMock
                .Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(properties);

        private static List<Property> SomeProperties() =>
        [
            MakeProperty(id: 1, price: 400_000m, commute: 10),
            MakeProperty(id: 2, price: 750_000m, commute: 44, schoolRating: 40, crimeScore: 60),
            MakeProperty(id: 3, price: 550_000m, commute: 25, roof: 55, floodRisk: 40),
            MakeProperty(id: 4, price: 610_000m, commute: 31, walkability: 90, noiseLevel: 70),
            MakeProperty(id: 5, price: 300_000m, commute: 58, ac: 30)
        ];

        private static string Serialize(IEnumerable<ScoredPropertyDto> dtos) => JsonSerializer.Serialize(dtos);

        private static HomeSearchPreferencesDto MakePrefs() => new()
        {
            MaxPrice = 800_000m,
            MaxMonthlyBudget = 4_500m,
            MinBedrooms = 2,
            MinBathrooms = 1,
            MinSqft = 1_000,
            MaxCommuteMin = 45,
            WeightAffordability = 0.20,
            WeightNeighborhood = 0.15,
            WeightSize = 0.10,
            WeightAppreciation = 0.10,
            WeightCondition = 0.10,
            WeightCommute = 0.10,
            WeightAmenities = 0.10,
            WeightTaxUtilities = 0.05,
            WeightResale = 0.05,
            WeightEnvironment = 0.05
        };

        private static Property MakeProperty(
            int id = 1,
            decimal price = 500_000m,
            int commute = 20,
            int schoolRating = 75,
            int crimeScore = 25,
            int walkability = 60,
            int transitAccess = 50,
            int roof = 80,
            int ac = 75,
            int plumbing = 85,
            int electrical = 82,
            int floodRisk = 15,
            int noiseLevel = 25) => new()
            {
                Id = id,
                Price = price,
                PropertyTax = 6_000m,
                HoaFee = 100m,
                Utilities = 120m,
                LotSqft = 1_800,
                Bedrooms = 3,
                Bathrooms = 2,
                CommuteMin = commute,
                SchoolRating = schoolRating,
                CrimeScore = crimeScore,
                Walkability = walkability,
                TransitAccess = transitAccess,
                AmenitiesScore = 65,
                FutureAppreciation = 60,
                ResalePotential = 55,
                RoofCondition = roof,
                AcCondition = ac,
                PlumbingCondition = plumbing,
                ElectricalCondition = electrical,
                FloodRisk = floodRisk,
                NoiseLevel = noiseLevel
            };
    }
}
