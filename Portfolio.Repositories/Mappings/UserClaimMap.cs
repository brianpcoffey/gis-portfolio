using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class UserClaimMap : IEntityTypeConfiguration<UserClaim>
    {
        public void Configure(EntityTypeBuilder<UserClaim> builder)
        {
            builder.ToTable("UserClaims");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClaimType)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.ClaimValue)
                .IsRequired();

            builder.HasIndex(x => new { x.UserId, x.ClaimType })
                .IsUnique(false);
        }
    }
}