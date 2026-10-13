namespace Portfolio.Services.Native
{
    /// <summary>
    /// Global kill switch: <c>PORTFOLIO_DISABLE_NATIVE</c> set to <c>1</c>, <c>true</c> or <c>yes</c> makes every
    /// bridge report <c>IsAvailable == false</c>, so the managed fallbacks run even when the libraries load.
    /// </summary>
    /// <remarks>
    /// Bridges probe once, at type initialisation, so the fallback could otherwise be exercised only by moving the
    /// libraries. The switch lets the benchmarks measure both paths, and lets the fallback (production behaviour
    /// today) be verified without a redeploy. Read once per process: set it before the process starts.
    /// </remarks>
    internal static class NativeToggle
    {
        private const string EnvironmentVariable = "PORTFOLIO_DISABLE_NATIVE";

        private static readonly bool _disabled = Read();

        /// <summary>True when native kernels have been explicitly disabled by environment variable.</summary>
        internal static bool Disabled => _disabled;

        private static bool Read()
        {
            try
            {
                var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
                if (string.IsNullOrWhiteSpace(value))
                    return false;

                value = value.Trim();
                return value.Equals("1", StringComparison.Ordinal)
                    || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Reading environment variables can throw under restricted trust; the safe
                // default is to leave the native path enabled and let TryLoad decide.
                return false;
            }
        }
    }
}
