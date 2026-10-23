using backend.Domain.RoadMaps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.RoadMaps
{
    public class RoadMapTemplateConfiguration : IEntityTypeConfiguration<RoadMapTemplate>
    {
        public void Configure(EntityTypeBuilder<RoadMapTemplate> builder)
        {
            builder.ToTable("roadmap_templates");

            //  Primary Key
            builder.HasKey(r => r.Id);

            //  Properties
            builder.Property(r => r.Name)
                   .HasColumnName("name")
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(r => r.Description)
                   .HasColumnName("description")
                   .HasMaxLength(1000);

            builder.Property(r => r.IsActive)
                   .HasColumnName("is_active")
                   .IsRequired()
                   .HasDefaultValue(true);

            //  Foreign Key
            builder.Property(r => r.ProductId)
                   .HasColumnName("product_id")
                   .IsRequired();

            builder.HasOne(r => r.Product)
                   .WithMany()
                   .HasForeignKey(r => r.ProductId)
                   .OnDelete(DeleteBehavior.Restrict);

            //  Indexes
            builder.HasIndex(r => r.ProductId);
            builder.HasIndex(r => r.IsActive);
            builder.HasIndex(r => r.Name)
                   .HasDatabaseName("ix_roadmap_templates_name");
        }
    }
}
