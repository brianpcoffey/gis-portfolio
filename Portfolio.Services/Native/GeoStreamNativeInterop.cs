using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    internal static class GeoStreamNativeInterop
    {
        private const string LibName = "geostream_processor";

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
