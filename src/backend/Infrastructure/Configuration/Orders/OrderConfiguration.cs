using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Orders
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders");

            builder.HasKey(o => o.Id);

            builder.HasOne(o => o.Client)
                   .WithMany(c => c.Orders)
                   .HasForeignKey(o => o.ClientId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(o => o.ClientId)
                   .HasColumnName("client_id")
                   .HasMaxLength(26)
                   .IsRequired();

            builder.Property(o => o.CreatedAt)
                   .HasColumnName("created_at")
                   .IsRequired();

            builder.Property(o => o.Source)
                   .HasColumnName("source")
                   .HasConversion<string>()
                   .HasMaxLength(32)
                   .IsRequired();

            builder.Property(x => x.Status)
       .HasConversion(
           v => v.Value,
           v => OrderStatus.FromValue(v))
       .HasColumnName("status")
       .HasMaxLength(32)
       .IsRequired();

            builder.Ignore(o => o.TotalAmount);

            builder.Navigation(o => o.Items).AutoInclude(false);
            builder.Navigation(o => o.Payments).AutoInclude(false);
            builder.Navigation(o => o.AppliedBenefits).AutoInclude(false);
        }
    }
}
