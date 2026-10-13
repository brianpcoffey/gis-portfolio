using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Portfolio.Services.Native
{
    /// <summary>What <see cref="NativeKernelLoader.Load"/> found.</summary>
    internal enum NativeLoadResult
    {
        /// <summary>The library loaded and, where a version is checked, reports the version this assembly expects.</summary>
        Loaded,

        /// <summary>Native is disabled, or the library is missing or cannot be loaded: the managed path runs.</summary>
        Unavailable,

        /// <summary>
        /// The library loaded but reports another ABI version, or has no version export. It was built from
        /// different sources than this assembly, so it is never called.
        /// </summary>
        AbiMismatch
    }

    /// <summary>
    /// Shared probe and failure classification for every native-kernel bridge.
    /// </summary>
    internal static class NativeKernelLoader
    {
        /// <summary>
        /// True when the named kernel library loads; always false when <see cref="NativeToggle.Disabled"/> is set or
        /// the probe throws. Does not check an ABI version; see <see cref="Load"/>.
        /// </summary>
        internal static bool TryLoad(string libraryName)
        {
            return Load(libraryName) == NativeLoadResult.Loaded;
        }

        /// <summary>
        /// Loads the named kernel library and, when <paramref name="abiVersionProbe"/> is given, requires it to
        /// return <paramref name="expectedAbiVersion"/>.
        /// </summary>
        /// <remarks>
        /// A kernel whose exported signatures changed keeps its export names, so a library built before the change
        /// still loads and resolves every entry point; calling it with the new argument list reads garbage as
        /// pointers and lengths. The version export is the only thing that tells the two apart, which is why a
        /// kernel that changes its ABI exports one and its bridge passes the probe here. The probe is the kernel's
        /// P/Invoke declaration (or a stand-in in a test).
        /// </remarks>
        internal static NativeLoadResult Load(string libraryName, Func<int>? abiVersionProbe = null, int expectedAbiVersion = 0)
        {
            try
            {
                if (NativeToggle.Disabled)
                    return NativeLoadResult.Unavailable;

                // All bridges live in this assembly, so the assembly and search-path arguments are shared.
                var loaded = NativeLibrary.TryLoad(
                    libraryName,
                    typeof(NativeKernelLoader).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
                if (!loaded)
                    return NativeLoadResult.Unavailable;
            }
            catch
            {
                return NativeLoadResult.Unavailable;
            }

            return abiVersionProbe is null
                ? NativeLoadResult.Loaded
                : CheckAbiVersion(abiVersionProbe, expectedAbiVersion);
        }

        /// <summary>
        /// Runs <paramref name="abiVersionProbe"/>: <see cref="NativeLoadResult.Loaded"/> when it returns
        /// <paramref name="expectedAbiVersion"/>, <see cref="NativeLoadResult.AbiMismatch"/> for another version or a
        /// missing export (a library older than the version function), and <see cref="NativeLoadResult.Unavailable"/>
        /// for any other failure.
        /// </summary>
        internal static NativeLoadResult CheckAbiVersion(Func<int> abiVersionProbe, int expectedAbiVersion)
        {
            try
            {
                return abiVersionProbe() == expectedAbiVersion
                    ? NativeLoadResult.Loaded
                    : NativeLoadResult.AbiMismatch;
            }
            catch (EntryPointNotFoundException)
            {
                return NativeLoadResult.AbiMismatch;
            }
            catch
            {
                return NativeLoadResult.Unavailable;
            }
        }

        /// <summary>Logs, as a warning, that a library was refused for its ABI version; a bridge calls it from its LogAvailability.</summary>
        internal static void LogAbiMismatch(ILogger logger, string libraryName, int expectedAbiVersion)
        {
            logger.LogWarning(
                "Native library {Library} was built for a different ABI than this application (version {ExpectedAbiVersion} expected) "
                + "and is not used; rebuild the native kernels (see Building the kernels in README.md). Using the managed implementation.",
                libraryName,
                expectedAbiVersion);
        }

        // InvalidOperationException is included on purpose: bridges throw it for a negative native
        // status or an out-of-range result, and that must log a warning and fall back to managed code.
        // Known wart: a genuine native bug that surfaces as an InvalidOperationException is
        // therefore also absorbed by the fallback (one logged warning, then the managed path
        // answers), not rethrown. The pattern is kept for consistency across the bridges.
        internal static bool IsNativeInvocationException(Exception exception)
        {
            return exception is DllNotFoundException
                or EntryPointNotFoundException
                or BadImageFormatException
                or SEHException
                or InvalidOperationException;
        }
    }
}
