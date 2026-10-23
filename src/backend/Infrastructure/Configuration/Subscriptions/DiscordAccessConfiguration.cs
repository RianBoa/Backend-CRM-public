using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUlid;

namespace backend.Infrastructure.Configurations.Subscriptions
{
    public class DiscordAccessConfiguration : IEntityTypeConfiguration<DiscordAccess>
    {
        public void Configure(EntityTypeBuilder<DiscordAccess> builder)
        {
            builder.ToTable("discord_accesses");

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

            builder.Property(a => a.DiscordUserId)
                .HasColumnName("discord_user_id")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(a => a.RoleId)
                .HasColumnName("role_id")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(a => a.RoleName)
                .HasColumnName("role_name")
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(a => a.GrantedAt)
                .HasColumnName("granted_at");

            builder.HasOne(a => a.Subscription)
                .WithMany(s => s.DiscordAccesses)
                .HasForeignKey(a => a.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.HasIndex(a => a.SubscriptionId);
            builder.HasIndex(a => a.DiscordUserId);
        }
    }
}
