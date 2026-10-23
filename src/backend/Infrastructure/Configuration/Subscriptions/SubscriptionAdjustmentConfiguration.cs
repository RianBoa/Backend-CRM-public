using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUlid;

namespace backend.Infrastructure.Configurations.Subscriptions
{
    public class SubscriptionAdjustmentConfiguration : IEntityTypeConfiguration<SubscriptionAdjustment>
    {
        public void Configure(EntityTypeBuilder<SubscriptionAdjustment> builder)
        {
            builder.ToTable("subscription_adjustments");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Id)
                .HasColumnName("id")
                .HasConversion(
                    v => v.ToString(),
                    v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();

            builder.Property(a => a.SubscriptionId)
                .HasColumnName("subscription_id")
                .HasConversion(
                    v => v.ToString(),
                    v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();

            builder.Property(a => a.RelatedOrderId)
                .HasColumnName("related_order_id")
                .HasConversion(
                    v => v != null ? v.Value.ToString() : null,
                    v => v != null ? Ulid.Parse(v) : (Ulid?)null)
                .HasMaxLength(26);

            builder.Property(a => a.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(a => a.AdjustmentDate)
                .HasColumnName("adjustment_date")
                .IsRequired();

            builder.Property(a => a.DaysChanged)
                .HasColumnName("days_changed");

            builder.Property(a => a.OldEndDate)
                .HasColumnName("old_end_date");

            builder.Property(a => a.NewEndDate)
                .HasColumnName("new_end_date");

            builder.Property(a => a.NewRole)
                .HasColumnName("new_role")
                .HasMaxLength(100);

            builder.Property(a => a.Reason)
                .HasColumnName("reason")
                .HasMaxLength(500);

            builder.Property(a => a.Source)
                .HasColumnName("source")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(a => a.CreatedBy)
                .HasColumnName("created_by")
                .HasMaxLength(100);

            builder.Property(a => a.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.HasOne(a => a.Subscription)
                .WithMany(s => s.Adjustments)
                .HasForeignKey(a => a.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.SubscriptionId);
            builder.HasIndex(a => a.Type);
            builder.HasIndex(a => a.Source);
        }
    }
}
