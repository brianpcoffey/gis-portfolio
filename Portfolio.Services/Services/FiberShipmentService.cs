using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services;

public class FiberShipmentService : IFiberShipmentService
{
    private readonly IFiberShipmentRepository _shipmentRepo;
    private readonly IUserProfileService _userProfileService;

    public FiberShipmentService(
        IFiberShipmentRepository shipmentRepo,
        IUserProfileService userProfileService)
    {
        _shipmentRepo = shipmentRepo;
        _userProfileService = userProfileService;
    }

    private static readonly string[] AllowedStatuses = ["Pending", "In Transit", "Delivered", "Exception"];
    private const int MaxTextLength = 100;
    private const int MaxStateLength = 50;
    private const int MaxRoutePoints = 1000;

    private static void ValidateStatus(string? status, string paramName)
    {
        if (status is null || Array.IndexOf(AllowedStatuses, status) < 0)
            throw new ArgumentException(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.", paramName);
    }

    private static void ValidateShipment(FiberShipmentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateTextLengths(dto);

        ValidateStatus(dto.Status, nameof(dto));

        if (!double.IsFinite(dto.OriginLat) || !double.IsFinite(dto.OriginLng)
            || !double.IsFinite(dto.DestinationLat) || !double.IsFinite(dto.DestinationLng))
            throw new ArgumentException("Coordinates must be finite numbers.", nameof(dto));

        ValidateRoute(dto);
    }

    private static void ValidateTextLengths(FiberShipmentDto dto)
    {
        if (dto.TrackingNumber?.Length > MaxTextLength)
            throw new ArgumentException($"TrackingNumber must be {MaxTextLength} characters or fewer.", nameof(dto));
        if (dto.CarrierName?.Length > MaxTextLength)
            throw new ArgumentException($"CarrierName must be {MaxTextLength} characters or fewer.", nameof(dto));
        if (dto.DestinationCity?.Length > MaxTextLength)
            throw new ArgumentException($"DestinationCity must be {MaxTextLength} characters or fewer.", nameof(dto));
        if (dto.DestinationState?.Length > MaxStateLength)
            throw new ArgumentException($"DestinationState must be {MaxStateLength} characters or fewer.", nameof(dto));
    }

    private static void ValidateRoute(FiberShipmentDto dto)
    {
        if (dto.Route is { Count: > MaxRoutePoints })
            throw new ArgumentException($"Route can have at most {MaxRoutePoints} points.", nameof(dto));
        if (dto.Route is not null && dto.Route.Any(p => p is null || !double.IsFinite(p.Lat) || !double.IsFinite(p.Lng)))
            throw new ArgumentException("Route points must have finite coordinates.", nameof(dto));
    }

    private Guid currentUserId =>
        _userProfileService.GetCurrentUserId() ?? throw new InvalidOperationException("User not identified");

    public async Task<List<FiberShipmentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await _shipmentRepo.GetAllAsync(currentUserId, cancellationToken);
        return shipments.Select(MapToDto).ToList();
    }

    public async Task<FiberShipmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var shipment = await _shipmentRepo.GetByIdAsync(id, currentUserId, cancellationToken);
        return shipment == null ? null : MapToDto(shipment);
    }

    public async Task<FiberShipmentDto> CreateAsync(FiberShipmentDto dto, CancellationToken cancellationToken = default)
    {
        ValidateShipment(dto);
        var shipment = new FiberShipment
        {
            UserId = currentUserId,
            TrackingNumber = dto.TrackingNumber,
            CarrierName = dto.CarrierName,
            Status = dto.Status,
            DestinationCity = dto.DestinationCity,
            DestinationState = dto.DestinationState,
            OriginLat = dto.OriginLat,
            OriginLng = dto.OriginLng,
            DestinationLat = dto.DestinationLat,
            DestinationLng = dto.DestinationLng,
            EstimatedArrival = dto.EstimatedArrival,
            RouteJson = dto.Route != null && dto.Route.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(dto.Route) : null
        };
        var created = await _shipmentRepo.AddAsync(shipment, cancellationToken);
        return MapToDto(created);
    }

    public async Task<FiberShipmentDto> UpdateAsync(int id, FiberShipmentDto dto, CancellationToken cancellationToken = default)
    {
        ValidateShipment(dto);
        var updated = await _shipmentRepo.UpdateAsync(id, dto, currentUserId, cancellationToken);
        return MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _shipmentRepo.DeleteAsync(id, currentUserId, cancellationToken);
    }

    public async Task<FiberShipmentDto> UpdateStatusAsync(int id, UpdateShipmentStatusDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateStatus(dto.Status, nameof(dto));
        var updated = await _shipmentRepo.UpdateStatusAsync(id, dto.Status, currentUserId, cancellationToken);
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
