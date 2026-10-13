using System.Net;
using Portfolio.E2E.Infrastructure;

namespace Portfolio.E2E.Tests;

/// <summary>
/// The rule behind "an outage skips, a retired or blocked URL fails". It needs no network or browser, so it
/// always runs: a mistake here could hide a broken production map behind a skip.
/// </summary>
public class CdnPreflightTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, CdnVerdict.Reachable)]
    [InlineData(HttpStatusCode.NoContent, CdnVerdict.Reachable)]
    [InlineData(HttpStatusCode.InternalServerError, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.BadGateway, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.GatewayTimeout, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.RequestTimeout, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.TooManyRequests, CdnVerdict.Outage)]
    [InlineData(HttpStatusCode.Forbidden, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.NotFound, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.Gone, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.MovedPermanently, CdnVerdict.Rejected)]
    [InlineData(HttpStatusCode.NotModified, CdnVerdict.Rejected)]
    public void Classify_Status_ReturnsTheExpectedVerdict(HttpStatusCode status, CdnVerdict expected)
    {
        // Arrange: the status is the whole input.

        // Act
        var verdict = CdnPreflight.Classify(status);

        // Assert
        Assert.Equal(expected, verdict);
    }
}
