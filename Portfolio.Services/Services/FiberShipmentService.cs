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
        CarrierName = s.CarrierName,
        TrackingNumber = s.TrackingNumber,
        Status = s.Status,
        ShipDate = s.ShipDate,
        EstimatedArrival = s.EstimatedArrival,
        OriginLat = s.OriginLat,
        OriginLng = s.OriginLng,
        DestinationLat = s.DestinationLat,
        DestinationLng = s.DestinationLng,
        DestinationCity = s.DestinationCity,
        DestinationState = s.DestinationState,
        OrderId = s.OrderId,
        ClientName = s.Order?.Client?.Name ?? string.Empty
    };
}
