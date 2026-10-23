using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Orders
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("order_items");

            //  Primary Key
            builder.HasKey(i => i.Id);

            //  Foreign Keys
            builder.Property(i => i.OrderId)
                .HasColumnName("order_id")
                .IsRequired();

            builder.Property(i => i.ProductId)
                .HasColumnName("product_id")
                .IsRequired();

            builder.Property(i => i.PackageId)
                .HasColumnName("package_id")
                .IsRequired();

            // Navigation
            builder.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Package)
                .WithMany()
                .HasForeignKey(i => i.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Properties
            builder.Property(i => i.Price)
                .HasColumnName("price")
                .HasPrecision(12, 2)
                .IsRequired();

            builder.Property(i => i.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            builder.Property(i => i.Meta)
                .HasColumnName("meta")
                .HasColumnType("jsonb"); // PostgreSQL

            // Indexes
            builder.HasIndex(i => i.OrderId);
            builder.HasIndex(i => i.ProductId);
            builder.HasIndex(i => i.PackageId);
        }
    }
}
