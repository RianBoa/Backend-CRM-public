using backend.Domain.Messaging;
using backend.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Messaging;

public sealed class DiscordGrantConfiguration : IEntityTypeConfiguration<DiscordGrantRequest>
{
    public void Configure(EntityTypeBuilder<DiscordGrantRequest> b)
    {
        b.ToTable("discord_grant_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.DiscordUserId).HasMaxLength(20).IsRequired();
        b.Property(x => x.GuildId).HasMaxLength(20).IsRequired();
        b.Property(x => x.RoleId).HasMaxLength(20).IsRequired();
        b.Property(x => x.RoleName).HasMaxLength(128).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsConcurrencyToken();
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.ResultFingerprint).HasMaxLength(64);
        b.HasIndex(x => x.ActiveSubscriptionId).IsUnique();
        b.HasIndex(x => new { x.SubscriptionId, x.RequestedAtUtc });
        b.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("integration_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.CorrelationId).HasMaxLength(32).IsRequired();
        b.Property(x => x.MessageType).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastErrorCode).HasMaxLength(64);
        b.Property(x => x.Attempts).IsConcurrencyToken();
        b.HasIndex(x => new { x.NextAttemptAtUtc, x.CreatedAtUtc, x.Id })
            .HasDatabaseName("ix_integration_outbox_ready")
            .HasFilter("\"PublishedAtUtc\" IS NULL AND \"DeadLetteredAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL");
        b.HasOne<DiscordGrantRequest>().WithOne().HasForeignKey<OutboxMessage>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InboxReceiptConfiguration : IEntityTypeConfiguration<InboxReceipt>
{
    public void Configure(EntityTypeBuilder<InboxReceipt> b)
    {
        b.ToTable("integration_inbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.HasOne<DiscordGrantRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
