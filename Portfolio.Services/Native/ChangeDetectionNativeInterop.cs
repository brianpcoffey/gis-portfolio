using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    // The change detection kernel carries no structs: every parameter is a flat,
    // contiguous blittable buffer, so there is no ChangeDetectionNativeStructs.cs.
    internal static class ChangeDetectionNativeInterop
    {
        private const string LibName = "change_detection_kernel";

        [DllImport(LibName, EntryPoint = "Change_ComputeCvaMagnitude", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int ComputeCvaMagnitude(
            [In] double[] epochA,
            [In] double[] epochB,
            int width,
            int height,
            int bandCount,
            [Out] double[] magnitude,
            int outputLength);

        [DllImport(LibName, EntryPoint = "Change_OtsuThreshold", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int OtsuThreshold(
            [In] double[] magnitude,
            int length,
            int binCount,
            [Out] double[] threshold,
            [Out] int[] histogram,
            int histogramCapacity);

        [DllImport(LibName, EntryPoint = "Change_MorphologicalOpen", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int MorphologicalOpen(
            [In] byte[] mask,
            int width,
            int height,
            int iterations,
            [Out] byte[] outMask,
            int outputLength);

        [DllImport(LibName, EntryPoint = "Change_LabelComponents", CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
        internal static extern int LabelComponents(
            [In] byte[] mask,
            int width,
            int height,
            [Out] int[] labels,
            int labelsLength,
            [Out] int[] blobAreas,
            [Out] double[] blobCentroidX,
            [Out] double[] blobCentroidY,
            [Out] double[] blobMeanMagnitude,
            [Out] int[] blobMinX,
            [Out] int[] blobMinY,
            [Out] int[] blobMaxX,
            [Out] int[] blobMaxY,
            [In] double[] magnitude,
            int blobCapacity);
    }
}
