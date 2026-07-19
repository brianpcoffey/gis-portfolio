using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    /// <summary>
    /// Reflection-based tests that guard against accidental removal of API versioning
    /// attributes on every controller.
    /// </summary>
    public class ApiVersioningAttributeTests
    {
        // ── [ApiVersion("1.0")] present on all versioned controllers ──────────

        [Theory]
        [InlineData(typeof(BatchGeocodingController))]
        [InlineData(typeof(ReverseGeocodingController))]
        [InlineData(typeof(AddressStandardizationController))]
        [InlineData(typeof(FeaturesController))]
        [InlineData(typeof(CollectionsController))]
        [InlineData(typeof(ProfileController))]
        [InlineData(typeof(UserProfileController))]
        [InlineData(typeof(SavedFeaturesController))]
        [InlineData(typeof(HomeFinderController))]
        [InlineData(typeof(FiberOrdersController))]
        [InlineData(typeof(FiberMaterialsController))]
        [InlineData(typeof(FiberShipmentsController))]
        [InlineData(typeof(FiberDashboardController))]
        public void Controller_HasApiVersionAttribute_WithV1(Type controllerType)
        {
            // Act
            var attr = controllerType
                .GetCustomAttributes(typeof(ApiVersionAttribute), inherit: false)
                .Cast<ApiVersionAttribute>()
                .SingleOrDefault();

            // Assert
            Assert.NotNull(attr);
            Assert.Single(attr.Versions);
            Assert.Equal(new ApiVersion(1, 0), attr.Versions[0]);
        }

        // ── [Route] versioned URL template ────────────────────────────────────

        [Theory]
        [InlineData(typeof(BatchGeocodingController), "api/v{version:apiVersion}/geocoding/batch")]
        [InlineData(typeof(ReverseGeocodingController), "api/v{version:apiVersion}/geocoding/reverse")]
        [InlineData(typeof(AddressStandardizationController), "api/v{version:apiVersion}/addresses")]
        [InlineData(typeof(FeaturesController), "api/v{version:apiVersion}/features")]
        [InlineData(typeof(CollectionsController), "api/v{version:apiVersion}/collections")]
        [InlineData(typeof(ProfileController), "api/v{version:apiVersion}/profile")]
        [InlineData(typeof(UserProfileController), "api/v{version:apiVersion}/users")]
        [InlineData(typeof(SavedFeaturesController), "api/v{version:apiVersion}/features/saved")]
        [InlineData(typeof(HomeFinderController), "api/v{version:apiVersion}/homefinder")]
        [InlineData(typeof(FiberOrdersController), "api/v{version:apiVersion}/fiber/orders")]
        [InlineData(typeof(FiberMaterialsController), "api/v{version:apiVersion}/fiber/materials")]
        [InlineData(typeof(FiberShipmentsController), "api/v{version:apiVersion}/fiber/shipments")]
        [InlineData(typeof(FiberDashboardController), "api/v{version:apiVersion}/fiber/dashboard")]
        public void Controller_HasExpectedVersionedRouteTemplate(Type controllerType, string expectedTemplate)
        {
            // Act
            var attr = controllerType
                .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .SingleOrDefault();

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(expectedTemplate, attr.Template);
        }

        // ── [AllowAnonymous] on anonymous-identity controllers ────────────────

        [Theory]
        [InlineData(typeof(BatchGeocodingController))]
        [InlineData(typeof(ReverseGeocodingController))]
        [InlineData(typeof(AddressStandardizationController))]
        [InlineData(typeof(FeaturesController))]
        [InlineData(typeof(SavedFeaturesController))]
        // HomeFinder scoring/search is a public demo; only its saved-search endpoints
        // carry method-level [Authorize].
        [InlineData(typeof(HomeFinderController))]
        public void Controller_StillHasAllowAnonymousAttribute(Type controllerType)
        {
            // Act
            var hasAttr = controllerType
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
                .Any();

            // Assert
            Assert.True(hasAttr, $"{controllerType.Name} is missing [AllowAnonymous].");
        }

        // ── [Authorize(Policy = "Authenticated")] on authenticated controllers ─

        [Theory]
        [InlineData(typeof(CollectionsController))]
        [InlineData(typeof(FiberOrdersController))]
        [InlineData(typeof(FiberMaterialsController))]
        [InlineData(typeof(FiberShipmentsController))]
        [InlineData(typeof(FiberDashboardController))]
        public void Controller_HasAuthorizeAttribute_WithAuthenticatedPolicy(Type controllerType)
        {
            // Act
            var attr = controllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>()
                .SingleOrDefault();

            // Assert
            Assert.NotNull(attr);
            Assert.Equal("Authenticated", attr.Policy);
        }
    }
}
