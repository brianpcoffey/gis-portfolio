using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    /// <summary>
    /// Shared probe and failure classification for every native-kernel bridge.
    /// </summary>
    internal static class NativeKernelLoader
    {
        /// <summary>
        /// True when the named kernel library loads. Always false when
        /// <see cref="NativeToggle.Disabled"/> is set or the probe throws. All bridges live in
        /// this assembly, so the assembly and search-path arguments are shared.
        /// </summary>
        internal static bool TryLoad(string libraryName)
        {
            try
            {
                if (NativeToggle.Disabled)
                    return false;

                return NativeLibrary.TryLoad(
                    libraryName,
                    typeof(NativeKernelLoader).Assembly,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory,
                    out _);
            }
            catch
            {
                return false;
            }
        }

        // InvalidOperationException is included on purpose: bridges throw it for a negative native
        // status or an out-of-range result, and that must log once and fall back to managed code.
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
