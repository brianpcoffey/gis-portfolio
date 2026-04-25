using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class SavedSearchMap : IEntityTypeConfiguration<SavedSearch>
    {
        public void Configure(EntityTypeBuilder<SavedSearch> builder)
        {
            builder.ToTable("SavedSearches");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.UserId).IsRequired();
            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.PreferencesJson).IsRequired().HasColumnType("jsonb");
            builder.Property(s => s.TopPropertyIds).HasMaxLength(500);
            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.LastModified);

            builder.HasIndex(s => s.UserId);
        }
    }
}