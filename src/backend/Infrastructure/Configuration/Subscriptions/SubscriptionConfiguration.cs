using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUlid;

namespace backend.Infrastructure.Configurations.Subscriptions
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("subscriptions");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id)
                .HasColumnName("id")
                .HasConversion(
                    v => v.ToString(),
                    v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();

            builder.Property(s => s.ClientId)
                .HasColumnName("client_id")
                .HasConversion(
                    v => v.ToString(),
                    v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();

            builder.Property(s => s.OrderItemId)
                .HasColumnName("order_item_id")
                .HasConversion(
                    v => v.ToString(),
                    v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();

            builder.Property(s => s.StartAt)
                .HasColumnName("start_at")
                .IsRequired(false);

            builder.Property(s => s.EndAt)
                .HasColumnName("end_at")
                .IsRequired(false);

            builder.Property(s => s.DurationDays).HasColumnName("duration_days");
            builder.Property(s => s.ProvisioningKey).HasColumnName("provisioning_key").HasMaxLength(80);
            builder.HasIndex(s => s.ProvisioningKey).IsUnique().HasDatabaseName("ux_subscription_provisioning_key");

            builder.Property(s => s.Notes)
                .HasColumnName("notes")
                .HasMaxLength(1000);

            builder.Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Status)
    .HasConversion<string>()
    .HasMaxLength(32)
    .IsConcurrencyToken()
    .IsRequired();

            builder.HasOne(s => s.Client)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(s => s.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.OrderItem)
                .WithMany()
                .HasForeignKey(s => s.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => s.ClientId);
            builder.HasIndex(s => s.ProductId);
            builder.HasIndex(s => s.Status);
        }
    }
}
