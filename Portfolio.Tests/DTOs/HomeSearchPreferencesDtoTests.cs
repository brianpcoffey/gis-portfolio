using System.ComponentModel.DataAnnotations;
using Portfolio.Common.DTOs;

namespace Portfolio.Tests.DTOs
{
    public class HomeSearchPreferencesDtoTests
    {
        private static List<ValidationResult> Validate(HomeSearchPreferencesDto dto)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
            return results;
        }

        private static HomeSearchPreferencesDto WithWeights(double w)
        {
            return new HomeSearchPreferencesDto
            {
                WeightAffordability = w,
                WeightNeighborhood = w,
                WeightSize = w,
                WeightAppreciation = w,
                WeightCondition = w,
                WeightCommute = w,
                WeightAmenities = w,
                WeightTaxUtilities = w,
                WeightResale = w,
                WeightEnvironment = w
            };
        }

        [Fact]
        public void Defaults_AreValidAndSumToOne()
        {
            // Arrange
            var dto = new HomeSearchPreferencesDto();

            // Act & Assert
            Assert.Equal(1.0, dto.AllWeights.Sum(), 10);
            Assert.Equal(10, dto.AllWeights.Count());
            Assert.Empty(Validate(dto));
        }

        [Fact]
        public void Validate_SumWithinTolerance_IsValid()
        {
            // Act & Assert
            // 10 * 0.1005 = 1.005, inside the +/-0.01 tolerance.
            Assert.Empty(Validate(WithWeights(0.1005)));
        }

        [Theory]
        [InlineData(0.05, "Weights must total 100%. Current total: 50%.")]
        [InlineData(0.15, "Weights must total 100%. Current total: 150%.")]
        [InlineData(0.0, "Weights must total 100%. Current total: 0%.")]
        public void Validate_SumNotAboutOne_ReportsTotalAgainstAllTenWeights(double each, string message)
        {
            // Act
            var results = Validate(WithWeights(each));

            // Assert
            var error = Assert.Single(results);
            Assert.Equal(message, error.ErrorMessage);
            Assert.Equal(10, error.MemberNames.Count());
            Assert.Contains(nameof(HomeSearchPreferencesDto.WeightAffordability), error.MemberNames);
            Assert.Contains(nameof(HomeSearchPreferencesDto.WeightEnvironment), error.MemberNames);
        }

        [Fact]
        public void Validate_SumJustOutsideTolerance_IsInvalid()
        {
            // Act & Assert
            // 10 * 0.1012 = 1.012 > 1.01
            var error = Assert.Single(Validate(WithWeights(0.1012)));
            Assert.StartsWith("Weights must total 100%.", error.ErrorMessage);
        }

        [Theory]
        [InlineData(1.5)]
        [InlineData(-0.1)]
        public void Validate_WeightOutOfRange_ReportsRangeAttributeAndTheRangeRule(double outOfRange)
        {
            // Arrange
            var dto = new HomeSearchPreferencesDto { WeightSize = outOfRange };

            // Act
            var results = Validate(dto);

            // Assert
            // [Range] on the property is reported by the validator, and IValidatableObject only
            // runs when property validation passes, so exactly one result is expected.
            var error = Assert.Single(results);
            Assert.Contains(nameof(HomeSearchPreferencesDto.WeightSize), error.MemberNames);
            Assert.Contains("between 0 and 1", error.ErrorMessage);
        }

        [Fact]
        public void Validate_DirectCallWithOutOfRangeWeight_ReportsOnlyTheRangeMessageAndStops()
        {
            // Arrange
            // Calling Validate() directly (as model binding does not) skips the attribute pass:
            // the per-weight guard yields one result, reported against a representative member,
            // and the sum check is not reached.
            var dto = new HomeSearchPreferencesDto { WeightSize = 1.5 };

            // Act
            var results = dto.Validate(new ValidationContext(dto)).ToList();

            // Assert
            var error = Assert.Single(results);
            Assert.Equal("Each weight must be between 0% and 100%.", error.ErrorMessage);
            Assert.Equal([nameof(HomeSearchPreferencesDto.WeightAffordability)], error.MemberNames);
        }

        [Fact]
        public void Validate_DirectCallWithNegativeWeight_ReportsOnlyTheRangeMessage()
        {
            // Arrange
            var dto = new HomeSearchPreferencesDto { WeightResale = -0.01 };

            // Act
            var error = Assert.Single(dto.Validate(new ValidationContext(dto)));

            // Assert
            Assert.Equal("Each weight must be between 0% and 100%.", error.ErrorMessage);
        }
    }
}
