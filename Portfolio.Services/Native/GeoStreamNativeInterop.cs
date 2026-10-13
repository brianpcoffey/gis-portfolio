using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class GeoStreamNativeInterop
    {
        private const string LibName = "geostream_processor";

        /// <summary>
        /// Bins <paramref name="eventCount"/> events into grid cells; the caller owns both arrays, and
        /// <paramref name="aggregates"/> must have at least <paramref name="aggregateCapacity"/> elements.
        /// Returns 0, or -1 (invalid argument), -2 (aggregate buffer too small) or -99 (native exception);
        /// the full contract is in geostream_processor.h.
        /// </summary>
        [DllImport(LibName, EntryPoint = "GeoStream_ProcessTelemetryBatch", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ProcessTelemetryBatch(
            [In] TelemetryEventNative[] events,
            int eventCount,
            in GeoStreamOptionsNative options,
            [Out] GridAggregateNative[] aggregates,
            int aggregateCapacity,
            out GeoStreamResultNative result);
    }
}
