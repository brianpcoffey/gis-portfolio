using Microsoft.AspNetCore.HttpOverrides;

/// <summary>Applies the configurable forwarded-header trust (internal static so tests can call it).</summary>
/// <remarks>
/// Production (appsettings.Production.json) sets ForwardLimit 3 and KnownNetworks for this hop chain,
/// measured with a temporary probe on 2026-10-13:
/// <code>
/// client -> Cloudflare edge -> Render load balancer (10.x) -> local proxy on loopback -> the app
/// X-Forwarded-For: "client, cloudflare-edge, render-10.x"        (connecting peer: loopback)
/// </code>
/// The middleware walks X-Forwarded-For from the right, taking one entry per trusted hop, up to ForwardLimit.
/// Loopback (a framework default that is kept whenever networks are configured), 10.0.0.0/8 (Render)
/// and Cloudflare's published ranges are trusted, so three entries are consumed and RemoteIpAddress ends
/// up as the client. Entries a caller prepends sit to the left of the client and are never reached.
/// ForwardLimit is 3 because that is the number of entries the two proxies and the client contribute.
/// The Cloudflare ranges come from https://www.cloudflare.com/ips-v4 and /ips-v6 (fetched 2026-10-13).
/// To refresh them, copy both lists into ForwardedHeaders:KnownNetworks in appsettings.Production.json
/// (keep 10.0.0.0/8) and into CloudflareRanges in ProductionForwardedHeadersTests. An edge address
/// missing from the list stops the walk at that edge, so the rate-limit key degrades to the edge
/// address for that request. It is never a value the client controls. Development and tests configure
/// no networks and keep the clamped single-hop behaviour.
/// </remarks>
internal static class ForwardedHeadersSetup
{
    internal static void Apply(ForwardedHeadersOptions options, IConfiguration configuration, ILogger? logger = null)
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);

        var networks = ReadNetworks(configuration);
        if (networks.Count == 0)
        {
            // Today's behaviour: trust any proxy. See the comment at the registration.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            // With no trust list every client is a trusted proxy, so more than one hop would let
            // a caller choose its own rate-limit key. Hold the limit at 1 whatever the config says.
            if (options.ForwardLimit != 1)
            {
                logger?.LogWarning(
                    "ForwardedHeaders:ForwardLimit {Requested} ignored: it is forced to 1 while ForwardedHeaders:KnownNetworks is empty.",
                    options.ForwardLimit);
                options.ForwardLimit = 1;
            }
            return;
        }

        foreach (var cidr in networks)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
    }

    // Accepts an array (appsettings, or Name__0 environment variables) or a single
    // comma-separated string (one Render environment variable).
    private static List<string> ReadNetworks(IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders:KnownNetworks");
        var items = section.Get<string[]>() ?? [];
        if (items.Length == 0 && !string.IsNullOrWhiteSpace(section.Value))
            items = section.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
    }
}
