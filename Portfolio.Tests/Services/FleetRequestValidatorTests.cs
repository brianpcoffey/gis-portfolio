using Portfolio.Common.DTOs;
using Portfolio.Services.Services.Fleet;

namespace Portfolio.Tests.Services;

// The validator's messages are public 400 bodies, and the order of the checks decides which
// message a multiply-invalid request gets. Both are pinned here.
public class FleetRequestValidatorTests
{
    private static OptimizeRequestDto Valid() => new()
    {
        DepotLatitude = 34.06,
        DepotLongitude = -117.22,
        Stops = [new FleetStopDto { Id = 1, Latitude = 34.05, Longitude = -117.2, Demand = 10, ReadyMinutes = 480, DueMinutes = 900, ServiceMinutes = 5 }],
    };

    private static string MessageOf(OptimizeRequestDto request)
    {
        var ex = Assert.Throws<ArgumentException>(() => FleetRequestValidator.Validate(request));
        Assert.Equal("request", ex.ParamName);
        return ex.Message.Split(" (Parameter")[0];
    }

    [Fact]
    public void Validate_ValidRequest_DoesNotThrow() => FleetRequestValidator.Validate(Valid());

    public static TheoryData<string, Action<OptimizeRequestDto>> Cases => new()
    {
        { "At least one stop is required.", r => r.Stops = [] },
        { "Routing is limited to 120 stops.", r => r.Stops = [.. Enumerable.Range(0, 121).Select(i => new FleetStopDto { Id = i, Demand = 1, DueMinutes = 900 })] },
        { "Vehicle count must be between 1 and 20.", r => r.VehicleCount = 0 },
        { "Vehicle count must be between 1 and 20.", r => r.VehicleCount = 21 },
        { "Vehicle capacity must be greater than zero.", r => r.VehicleCapacity = 0 },
        { "Vehicle capacity must be greater than zero.", r => r.VehicleCapacity = double.NaN },
        { "Shift start must be earlier than shift end.", r => r.ShiftStartMinutes = r.ShiftEndMinutes },
        { "Vehicle fixed cost cannot be negative.", r => r.VehicleFixedCost = -1 },
        { "Iterations must be between 1 and 5000.", r => r.MaxIterations = 5_001 },
        { "Stop coordinates must be finite values.", r => r.DepotLatitude = double.PositiveInfinity },
        { "Stop coordinates must be finite values.", r => r.Stops[0].Longitude = double.NaN },
        { "Stop demand must be greater than zero.", r => r.Stops[0].Demand = 0 },
        { "No vehicle can serve a stop whose demand exceeds capacity.", r => r.Stops[0].Demand = r.VehicleCapacity + 1 },
        { "Stop ready time must not be later than its due time.", r => r.Stops[0].ReadyMinutes = 901 },
        { "Stop service time cannot be negative.", r => r.Stops[0].ServiceMinutes = -1 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Validate_InvalidRequest_ThrowsVerbatimMessage(string expected, Action<OptimizeRequestDto> mutate)
    {
        var request = Valid();
        mutate(request);

        Assert.Equal(expected, MessageOf(request));
    }

    [Fact]
    public void Validate_FleetChecksRunBeforeScheduleChecks()
    {
        var request = Valid();
        request.VehicleCount = 0;
        request.ShiftStartMinutes = request.ShiftEndMinutes;

        Assert.Equal("Vehicle count must be between 1 and 20.", MessageOf(request));
    }

    [Fact]
    public void Validate_ScheduleChecksRunBeforeStopChecks()
    {
        var request = Valid();
        request.VehicleFixedCost = -1;
        request.Stops[0].Demand = 0;

        Assert.Equal("Vehicle fixed cost cannot be negative.", MessageOf(request));
    }
}
