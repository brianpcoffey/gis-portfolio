using Portfolio.Common.DTOs;

namespace Portfolio.Services.Services.Fleet;

/// <summary>
/// Request guards for the fleet optimizer; messages are public 400 bodies, so keep them verbatim.
/// </summary>
internal static class FleetRequestValidator
{
    private const int MaxStops = 120;
    private const int MaxVehicles = 20;
    private const int MaxIterations = 5_000;

    internal static void Validate(OptimizeRequestDto request)
    {
        ValidateFleet(request);
        ValidateSchedule(request);

        foreach (var stop in request.Stops)
            ValidateStop(request, stop);
    }

    private static void ValidateFleet(OptimizeRequestDto request)
    {
        if (request.Stops is null || request.Stops.Count == 0)
            throw new ArgumentException("At least one stop is required.", nameof(request));
        if (request.Stops.Count > MaxStops)
            throw new ArgumentException($"Routing is limited to {MaxStops} stops.", nameof(request));
        if (request.VehicleCount < 1 || request.VehicleCount > MaxVehicles)
            throw new ArgumentException($"Vehicle count must be between 1 and {MaxVehicles}.", nameof(request));
        if (!double.IsFinite(request.VehicleCapacity) || request.VehicleCapacity <= 0)
            throw new ArgumentException("Vehicle capacity must be greater than zero.", nameof(request));
    }

    private static void ValidateSchedule(OptimizeRequestDto request)
    {
        if (!double.IsFinite(request.ShiftStartMinutes) || !double.IsFinite(request.ShiftEndMinutes) ||
            request.ShiftStartMinutes >= request.ShiftEndMinutes)
            throw new ArgumentException("Shift start must be earlier than shift end.", nameof(request));
        if (!double.IsFinite(request.VehicleFixedCost) || request.VehicleFixedCost < 0)
            throw new ArgumentException("Vehicle fixed cost cannot be negative.", nameof(request));
        if (request.MaxIterations < 1 || request.MaxIterations > MaxIterations)
            throw new ArgumentException($"Iterations must be between 1 and {MaxIterations}.", nameof(request));
        if (!double.IsFinite(request.DepotLatitude) || !double.IsFinite(request.DepotLongitude))
            throw new ArgumentException("Stop coordinates must be finite values.", nameof(request));
    }

    private static void ValidateStop(OptimizeRequestDto request, FleetStopDto stop)
    {
        if (!double.IsFinite(stop.Latitude) || !double.IsFinite(stop.Longitude))
            throw new ArgumentException("Stop coordinates must be finite values.", nameof(request));
        if (!double.IsFinite(stop.Demand) || stop.Demand <= 0)
            throw new ArgumentException("Stop demand must be greater than zero.", nameof(request));
        if (stop.Demand > request.VehicleCapacity)
            throw new ArgumentException("No vehicle can serve a stop whose demand exceeds capacity.", nameof(request));
        if (!double.IsFinite(stop.ReadyMinutes) || !double.IsFinite(stop.DueMinutes) || stop.ReadyMinutes > stop.DueMinutes)
            throw new ArgumentException("Stop ready time must not be later than its due time.", nameof(request));
        if (!double.IsFinite(stop.ServiceMinutes) || stop.ServiceMinutes < 0)
            throw new ArgumentException("Stop service time cannot be negative.", nameof(request));
    }
}
