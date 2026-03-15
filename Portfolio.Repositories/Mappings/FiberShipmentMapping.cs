using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings;

public class FiberShipmentMapping : IEntityTypeConfiguration<FiberShipment>
{
    public void Configure(EntityTypeBuilder<FiberShipment> builder)
    {
        builder.ToTable("FiberShipments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CarrierName).HasColumnName("carrier_name");
        builder.Property(x => x.TrackingNumber).HasColumnName("tracking_number");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.EstimatedArrival).HasColumnName("estimated_arrival");
        builder.Property(x => x.OriginLat).HasColumnName("origin_lat");
        builder.Property(x => x.OriginLng).HasColumnName("origin_lng");
        builder.Property(x => x.DestinationLat).HasColumnName("destination_lat");
        builder.Property(x => x.DestinationLng).HasColumnName("destination_lng");
        builder.Property(x => x.DestinationCity).HasColumnName("destination_city");
        builder.Property(x => x.DestinationState).HasColumnName("destination_state");
        builder.Property(x => x.RouteJson).HasColumnName("route_json");
    }
}
