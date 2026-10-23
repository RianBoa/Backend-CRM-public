using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUlid;

namespace backend.Infrastructure.Configurations.Subscriptions
{
    public class PlatformAccessConfiguration : IEntityTypeConfiguration<PlatformAccess>
    {
        public void Configure(EntityTypeBuilder<PlatformAccess> builder)
        {
            builder.ToTable("platform_accesses");

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

            builder.Property(a => a.PlatformName)
                .HasColumnName("platform_name")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(a => a.PlatformUserId)
                .HasColumnName("platform_user_id")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(a => a.GrantedAt)
                .HasColumnName("granted_at");

            builder.Property(a => a.Notes)
                .HasColumnName("notes")
                .HasMaxLength(500);

            builder.HasOne(a => a.Subscription)
                .WithMany(s => s.PlatformAccesses)
                .HasForeignKey(a => a.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.SubscriptionId);
            builder.HasIndex(a => a.PlatformUserId);
        }
    }
}
