namespace Portfolio.Services.Native
{
    /// <summary>
    /// Global kill switch for every native kernel.
    ///
    /// Setting <c>PORTFOLIO_DISABLE_NATIVE</c> to <c>1</c>, <c>true</c> or <c>yes</c> makes
    /// every bridge report <c>IsAvailable == false</c>, so the managed fallbacks run even
    /// when the shared libraries are present and loadable.
    ///
    /// This exists for two reasons. The benchmark harness needs to measure both paths from
    /// one build, and each bridge probes availability exactly once in a static constructor —
    /// so the only other way to exercise the fallback is to physically move the libraries.
    /// It also makes the fallback verifiable in a deployed environment without a redeploy,
    /// which matters because the managed path is production behaviour today.
    /// </summary>
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
