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

        /// <summary>
        /// Score a single property.  Writes one <see cref="ScoreOutputNative"/> to <paramref name="result"/>.
        /// </summary>
        [DllImport(LibName,
            EntryPoint        = "ScoreProperty",
            CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory
                                   | DllImportSearchPath.ApplicationDirectory)]
        internal static extern void ScoreProperty(
            in  PropertyInputNative    property,
            in  PreferencesInputNative prefs,
            out ScoreOutputNative      result);

        /// <summary>
        /// Score <paramref name="count"/> properties in one native call.
        /// <paramref name="properties"/> and <paramref name="scores"/> must each have
        /// at least <paramref name="count"/> elements.
        /// </summary>
        [DllImport(LibName,
            EntryPoint        = "ScorePropertyBatch",
            CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory
                                   | DllImportSearchPath.ApplicationDirectory)]
        internal static extern void ScorePropertyBatch(
            [In]  PropertyInputNative[]    properties,
            int                            count,
            in    PreferencesInputNative   prefs,
            [Out] ScoreOutputNative[]      scores);
    }
}
