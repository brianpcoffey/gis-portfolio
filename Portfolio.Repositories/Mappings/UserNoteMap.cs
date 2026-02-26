using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Common.Models;

namespace Portfolio.Repositories.Mappings
{
    public class UserNoteMap : IEntityTypeConfiguration<UserNote>
    {
        public void Configure(EntityTypeBuilder<UserNote> builder)
        {
            builder.ToTable("UserNotes");
            builder.HasKey(n => n.Id);

            builder.Property(n => n.UserId).IsRequired();
            builder.Property(n => n.SavedFeatureId).IsRequired();
            builder.Property(n => n.Note).IsRequired().HasMaxLength(1000);
            builder.Property(n => n.CreatedAt).IsRequired();

            builder.HasOne(n => n.SavedFeature)
                   .WithMany(f => f.UserNotes)
                   .HasForeignKey(n => n.SavedFeatureId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}