using Microsoft.AspNetCore.HttpOverrides;

/// <summary>Applies the configurable forwarded-header trust. Internal static so tests can call it.</summary>
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
