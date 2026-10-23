using NUlid;

namespace backend.Domain.Messaging;

public enum DiscordGrantStatus { Queued, Succeeded, Failed, NeedsReview, Canceled }

public sealed class DiscordGrantRequest
{
    public Ulid Id { get; private init; }
    public Ulid SubscriptionId { get; private init; }
    public Ulid ClientId { get; private init; }
    public Ulid? ActiveSubscriptionId { get; private set; }
    public Ulid RequestedById { get; private init; }
    public string DiscordUserId { get; private init; } = null!;
    public string GuildId { get; private init; } = null!;
    public string RoleId { get; private init; } = null!;
    public string RoleName { get; private init; } = null!;
    public string CorrelationId { get; private init; } = null!;
    public DiscordGrantStatus Status { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private init; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ResultFingerprint { get; private set; }

    private DiscordGrantRequest() { }

    public static DiscordGrantRequest Queue(Ulid id, Ulid subscriptionId, Ulid clientId, Ulid actorId,
        string userId, string guildId, string roleId, string roleName, string correlationId, DateTimeOffset now)
        => new()
        {
            Id = id, SubscriptionId = subscriptionId, ClientId = clientId, ActiveSubscriptionId = subscriptionId,
            RequestedById = actorId, DiscordUserId = userId, GuildId = guildId, RoleId = roleId,
            RoleName = roleName, CorrelationId = correlationId, RequestedAtUtc = now,
            Status = DiscordGrantStatus.Queued
        };

    public void Complete(DiscordGrantStatus status, string fingerprint, DateTimeOffset now, string? errorCode = null)
    {
        if (Status != DiscordGrantStatus.Queued || status == DiscordGrantStatus.Queued)
            throw new InvalidOperationException("Only a queued grant can be completed.");
        Status = status;
        ResultFingerprint = fingerprint;
        CompletedAtUtc = now;
        ErrorCode = errorCode;
        ActiveSubscriptionId = null;
    }

    public void CancelBeforeDispatch(DateTimeOffset now)
    {
        if (Status != DiscordGrantStatus.Queued) return;
        Status = DiscordGrantStatus.Canceled;
        CompletedAtUtc = now;
        ActiveSubscriptionId = null;
    }

    public void RecordLateCanceledResult(string fingerprint, bool success, DateTimeOffset now)
    {
        if (Status != DiscordGrantStatus.Canceled || ResultFingerprint is not null)
            throw new InvalidOperationException("Only an unconfirmed canceled grant accepts a late result.");
        ResultFingerprint = fingerprint;
        CompletedAtUtc = now;
        if (success) { Status = DiscordGrantStatus.NeedsReview; ErrorCode = "subscription_state_changed"; }
    }
}
