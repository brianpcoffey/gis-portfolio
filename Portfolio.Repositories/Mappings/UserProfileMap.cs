using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class UserProfileMap : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("UserProfiles");

            builder.HasKey(x => x.UserId);

            builder.Property(x => x.CreatedDate).IsRequired();
            builder.Property(x => x.LastActiveDate).IsRequired();

            builder.HasMany(x => x.Claims)
                .WithOne(c => c.UserProfile)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}