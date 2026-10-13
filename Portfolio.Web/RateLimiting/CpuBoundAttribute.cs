namespace Portfolio.Web.RateLimiting;

/// <summary>Marks a controller whose actions are CPU-bound (native compute or heavy in-process work).</summary>
/// <remarks>
/// Such endpoints also pass through the process-wide shared concurrency limiter, so total CPU use stays
/// bounded however many client keys are in play. I/O-bound endpoints (the ArcGIS proxies) must NOT
/// carry it: they would hold a CPU slot for the whole upstream round trip.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CpuBoundAttribute : Attribute
{
}
