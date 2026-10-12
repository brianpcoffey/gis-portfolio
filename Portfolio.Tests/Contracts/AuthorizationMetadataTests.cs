using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Portfolio.Tests.Contracts;

/// <summary>
/// Guards the identity rule in agent-context section 6: every public controller action carries
/// [Authorize(Policy = "Authenticated")] or [AllowAnonymous], at the action or the class level,
/// and "Authenticated" is the only policy named. A class-level [AllowAnonymous] on a controller
/// that also has authorized actions would short-circuit authorization, so mixed controllers must
/// annotate each action instead.
/// </summary>
public class AuthorizationMetadataTests
{
    private static readonly Type[] Controllers = typeof(Program).Assembly.GetTypes()
        .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(ControllerBase).IsAssignableFrom(t))
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .ToArray();

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    [Fact]
    public void ControllersAreDiscovered()
    {
        // Act & Assert
        Assert.Equal(25, Controllers.Length);
        Assert.True(Controllers.SelectMany(Actions).Count() >= 74);
    }

    [Fact]
    public void EveryPublicAction_HasExplicitAuthorizationMetadata()
    {
        // Act
        var missing = new List<string>();
        foreach (var c in Controllers)
        {
            var classLevel = c.GetCustomAttribute<AuthorizeAttribute>() is not null ||
                             c.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            foreach (var m in Actions(c))
            {
                var actionLevel = m.GetCustomAttribute<AuthorizeAttribute>() is not null ||
                                  m.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
                if (!classLevel && !actionLevel) missing.Add($"{c.Name}.{m.Name}");
            }
        }

        // Assert
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryAuthorizeAttribute_UsesTheAuthenticatedPolicy()
    {
        // Act
        var wrong = new List<string>();
        foreach (var c in Controllers)
        {
            foreach (var a in c.GetCustomAttributes<AuthorizeAttribute>())
                if (a.Policy != "Authenticated" || a.Roles is not null || a.AuthenticationSchemes is not null) wrong.Add(c.Name);
            foreach (var m in Actions(c))
                foreach (var a in m.GetCustomAttributes<AuthorizeAttribute>())
                    if (a.Policy != "Authenticated" || a.Roles is not null || a.AuthenticationSchemes is not null) wrong.Add($"{c.Name}.{m.Name}");
        }

        // Assert
        Assert.Empty(wrong);
    }

    [Fact]
    public void MixedControllers_HaveNoClassLevelAllowAnonymous()
    {
        // Act
        // "Mixed" = at least one action carries its own Authorize or AllowAnonymous attribute.
        var mixed = Controllers
            .Where(c => Actions(c).Any(m => m.GetCustomAttribute<AuthorizeAttribute>() is not null ||
                                            m.GetCustomAttribute<AllowAnonymousAttribute>() is not null))
            .ToList();

        // Assert
        Assert.Equal(["HomeFinderController", "ProfileController", "UserProfileController"], mixed.Select(c => c.Name));
        Assert.All(mixed, c => Assert.Null(c.GetCustomAttribute<AllowAnonymousAttribute>()));
    }

    [Theory]
    [InlineData("GeoStreamController", "compute")]
    [InlineData("SpatialGeometryController", "compute")]
    [InlineData("RasterTerrainController", "compute")]
    [InlineData("SpatialNetworkController", "compute")]
    [InlineData("SpatialClusterController", "compute")]
    [InlineData("ViewshedController", "compute")]
    [InlineData("SpatialOverlayController", "compute")]
    [InlineData("FeaturesController", "expensive")]
    public void ComputeAndProxyControllers_CarryTheirRateLimitPolicy(string controller, string policy)
    {
        // Arrange
        var type = Controllers.Single(c => c.Name == controller);

        // Act
        var attr = type.GetCustomAttribute<EnableRateLimitingAttribute>();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal(policy, attr!.PolicyName);
    }
}
