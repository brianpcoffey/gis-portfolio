using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberShipmentService : IFiberShipmentService
{
    private readonly IFiberShipmentRepository _shipmentRepo;
    private readonly IUserProfileService _userProfileService;
    private readonly TimeProvider _timeProvider;

    public FiberShipmentService(
        IFiberShipmentRepository shipmentRepo,
        IUserProfileService userProfileService,
        TimeProvider timeProvider)
    {
        _shipmentRepo = shipmentRepo;
        _userProfileService = userProfileService;
        _timeProvider = timeProvider;
    }

    private Guid CurrentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<List<FiberShipmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await _shipmentRepo.GetAllAsync(CurrentUserId, cancellationToken);
        return shipments.Select(MapToDto).ToList();
    }

    public async Task<FiberShipmentDto> UpdateStatusAsync(int id, UpdateShipmentStatusDto dto, CancellationToken cancellationToken = default)
    {
        var updated = await _shipmentRepo.UpdateStatusAsync(id, dto.Status, CurrentUserId, cancellationToken);
        return MapToDto(updated);
    }

    private static FiberShipmentDto MapToDto(FiberShipment s) => new()
    {
        Id = s.Id,
        TrackingNumber = s.TrackingNumber,
        CarrierName = s.CarrierName,
        Status = s.Status,
        DestinationCity = s.DestinationCity,
        DestinationState = s.DestinationState,
        OriginLat = s.OriginLat,
        OriginLng = s.OriginLng,
        DestinationLat = s.DestinationLat,
        DestinationLng = s.DestinationLng,
        EstimatedArrival = s.EstimatedArrival,
        Route = string.IsNullOrEmpty(s.RouteJson)
            ? new List<RoutePointDto> {
                new RoutePointDto { Lat = s.OriginLat, Lng = s.OriginLng },
                new RoutePointDto { Lat = s.DestinationLat, Lng = s.DestinationLng }
            }
            : System.Text.Json.JsonSerializer.Deserialize<List<RoutePointDto>>(s.RouteJson) ?? new List<RoutePointDto>()
    };
}
