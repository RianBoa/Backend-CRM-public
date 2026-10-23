using backend.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Products
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(p => p.Code)
                .HasColumnName("code")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(p => p.LegacyCode)
                .HasColumnName("legacy_code")
                .HasMaxLength(64);

            builder.Property(p => p.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(p => p.Type)
                .HasColumnName("type")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(p => p.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            builder.Property(p => p.IsArchived)
                .HasColumnName("is_archived")
                .HasDefaultValue(false);

            builder.Property(p => p.DiscordAccess)
                .HasColumnName("discord_access")
                .HasDefaultValue(false);

            builder.Property(p => p.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.HasIndex(p => p.Code).IsUnique();
            builder.HasIndex(p => p.LegacyCode);
        }
    }
}
