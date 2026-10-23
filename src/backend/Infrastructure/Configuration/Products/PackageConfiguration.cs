using backend.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Products
{
    public class PackageConfiguration : IEntityTypeConfiguration<Package>
    {
        public void Configure(EntityTypeBuilder<Package> builder)
        {
            builder.ToTable("packages");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(p => p.Code)
                .HasColumnName("code")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(p => p.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(p => p.DurationDays)
                .HasColumnName("duration_days");

            builder.Property(p => p.DiscordAccess)
                .HasColumnName("discord_access")
                .HasDefaultValue(false);

            builder.Property(p => p.DiscordDays)
                .HasColumnName("discord_days");


            builder.HasOne(p => p.Product)
                   .WithMany()
                   .HasForeignKey(p => p.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(p => p.ProductId)
                .HasColumnName("product_id")
                .IsRequired();

            builder.HasIndex(p => p.Code).IsUnique();
        }
    }
}
