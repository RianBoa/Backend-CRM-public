using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.Orders
{
    public sealed class ConditionRuleConfiguration : IEntityTypeConfiguration<ConditionRule>
    {
        public void Configure(EntityTypeBuilder<ConditionRule> builder)
        {
            builder.ToTable("condition_rules");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.OrderItemId);
            builder.HasIndex(x => x.ProductId);
            builder.HasIndex(x => x.UpdatedAt);

            builder
                .HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder.Property(x => x.ConditionText)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(32);

            builder.Property(x => x.UpdatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
