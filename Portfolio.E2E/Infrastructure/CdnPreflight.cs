using System.Net;

namespace Portfolio.E2E.Infrastructure;

/// <summary>What the start-up pre-flight concluded from one answer about a pinned third-party URL.</summary>
public enum CdnVerdict
{
    /// <summary>A 2xx answer: the file is there.</summary>
    Reachable,

    /// <summary>A transient outage (5xx, 408 or 429): the tests that need the CDN skip.</summary>
    Outage,

    /// <summary>
    /// The CDN answered but did not serve the file (403, 404, 410 or any other non-success answer): the pinned
    /// version was retired or this client is blocked, which would break the production maps too. The tests fail.
    /// </summary>
    Rejected
}

/// <summary>The rule for turning an HTTP status into a <see cref="CdnVerdict"/>. Pure, so it is tested without a network.</summary>
public static class CdnPreflight
{
    /// <summary>
    /// Only an outage may excuse a test. A transport error or a timeout is an outage too, but never reaches here:
    /// there is no status to classify.
    /// </summary>
    public static CdnVerdict Classify(HttpStatusCode status)
    {
        var code = (int)status;

        if (code is >= 200 and < 300)
            return CdnVerdict.Reachable;

        if (code is >= 500 and < 600 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            return CdnVerdict.Outage;

        return CdnVerdict.Rejected;
    }
}
