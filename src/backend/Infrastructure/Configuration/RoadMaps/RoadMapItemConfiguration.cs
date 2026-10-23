using backend.Domain.RoadMaps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.RoadMaps
{
    public class RoadMapItemConfiguration : IEntityTypeConfiguration<RoadMapItem>
    {
        public void Configure(EntityTypeBuilder<RoadMapItem> builder)
        {
            builder.ToTable("roadmap_items");

            //  Primary Key
            builder.HasKey(i => i.Id);

            //  Foreign Key
            builder.Property(i => i.RoadMapTemplateId)
                   .HasColumnName("roadmap_template_id")
                   .IsRequired();

            builder.HasOne(i => i.RoadMapTemplate)
                   .WithMany()
                   .HasForeignKey(i => i.RoadMapTemplateId)
                   .OnDelete(DeleteBehavior.Cascade);

            //  Properties
            builder.Property(i => i.Title)
                   .HasColumnName("title")
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(i => i.Type)
                   .HasColumnName("type")
                   .HasConversion<string>()
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(i => i.DaysFromStart)
                   .HasColumnName("days_from_start")
                   .IsRequired();

            // Indexes
            builder.HasIndex(i => i.RoadMapTemplateId);
            builder.HasIndex(i => i.Type);
            builder.HasIndex(i => i.DaysFromStart);
        }
    }
}
