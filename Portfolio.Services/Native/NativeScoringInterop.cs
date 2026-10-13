using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    // Raw P/Invoke declarations for portfolio_scoring.(dll|so).
    // These are the only entry points; all callers should go through
    // NativeScoringBridge which checks availability before calling here.
    internal static class NativeScoringInterop
    {
        // Library name without extension or "lib" prefix.
        // On Windows the runtime appends .dll; on Linux it prepends lib and appends .so.
        // The AssemblyDirectory search path ensures the library is found next to
        // Portfolio.Services.dll regardless of working directory.
        private const string LibName = "portfolio_scoring";

        // Must equal PS_ABI_VERSION in native/portfolio_scoring/include/portfolio_scoring.h. NativeScoringBridge
        // refuses a library that reports another version (or none), so bump both together with any signature change.
        internal const int ExpectedAbiVersion = 2;

        [DllImport(LibName,
            EntryPoint = "Scoring_AbiVersion",
            CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory
                                   | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int AbiVersion();

        // Both entry points return a status code (portfolio_scoring.h): 0 on success, otherwise
        // -1 invalid argument (null pointer, negative count or length),
        // -3 output buffer smaller than the records to write, or -99 native exception.
        // NativeScoringBridge turns a non-zero status into an InvalidOperationException.

        /// <summary>
        /// Scores a single property, writing one <see cref="ScoreOutputNative"/> to <paramref name="result"/>.
        /// <paramref name="outputLength"/> is the capacity of <paramref name="result"/> in records (1 here).
        /// </summary>
        [DllImport(LibName,
            EntryPoint = "ScoreProperty",
            CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory
                                   | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ScoreProperty(
            in PropertyInputNative property,
            in PreferencesInputNative prefs,
            out ScoreOutputNative result,
            int outputLength);

        /// <summary>
        /// Scores <paramref name="count"/> properties in one native call; <paramref name="properties"/> must have
        /// at least <paramref name="count"/> elements, and <paramref name="scores"/> at least
        /// <paramref name="outputLength"/>, which must be at least <paramref name="count"/>.
        /// </summary>
        [DllImport(LibName,
            EntryPoint = "ScorePropertyBatch",
            CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory
                                   | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ScorePropertyBatch(
            [In] PropertyInputNative[] properties,
            int count,
            in PreferencesInputNative prefs,
            [Out] ScoreOutputNative[] scores,
            int outputLength);
    }
}
