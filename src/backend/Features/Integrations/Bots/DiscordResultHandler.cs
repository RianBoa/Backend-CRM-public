using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Domain.Auditing;
using backend.Domain.Messaging;
using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Integrations.Bots;

public enum ResultDisposition { Applied, Duplicate, Rejected }

public sealed class DiscordResultHandler(ApplicationDbContext db, TimeProvider clock, ILogger<DiscordResultHandler> logger)
{
    public async Task<ResultDisposition> HandleAsync(ReadOnlyMemory<byte> body, string? transportMessageId, CancellationToken ct)
    {
        if (body.Length > 65536) return ResultDisposition.Rejected;
        MessageEnvelope<DiscordRoleResult>? envelope;
        try { envelope = JsonSerializer.Deserialize<MessageEnvelope<DiscordRoleResult>>(body.Span, BotMessages.Json); }
        catch (JsonException) { return ResultDisposition.Rejected; }
        var now = clock.GetUtcNow();
        if (envelope?.Payload is not { } result || envelope.Version != 1 || envelope.Type != BotMessages.GrantResult ||
            envelope.MessageId != transportMessageId || !Ulid.TryParse(envelope.MessageId, out var messageId) || messageId == Ulid.Empty ||
            !Ulid.TryParse(result.RequestId, out var requestId) || requestId == Ulid.Empty ||
            !Guid.TryParseExact(envelope.CorrelationId, "N", out _) ||
            envelope.OccurredAtUtc < DateTimeOffset.UnixEpoch || envelope.OccurredAtUtc > now ||
            result.Outcome is not ("granted" or "already_granted" or "failed")) return ResultDisposition.Rejected;

        var success = result.Outcome != "failed";
        if (success && (result.ConfirmedAtUtc is null || result.ConfirmedAtUtc > now || result.ErrorCode is not null) ||
            !success && (result.ConfirmedAtUtc is not null || result.ErrorCode is null ||
                !Regex.IsMatch(result.ErrorCode, "^[a-z][a-z0-9_]{0,63}$"))) return ResultDisposition.Rejected;
        var fingerprint = Hash(envelope);
        var outcomeFingerprint = Hash(result);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var receipt = await db.IntegrationInbox.AsNoTracking().SingleOrDefaultAsync(x => x.Id == messageId, ct);
        if (receipt is not null)
            return receipt.Fingerprint == fingerprint ? ResultDisposition.Duplicate : ResultDisposition.Rejected;

        var request = await db.DiscordGrantRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null || request.CorrelationId != envelope.CorrelationId || request.DiscordUserId != result.DiscordUserId ||
            request.GuildId != result.GuildId || request.RoleId != result.RoleId ||
            success && result.ConfirmedAtUtc < request.RequestedAtUtc.AddMinutes(-5)) return ResultDisposition.Rejected;
        var lateCanceledResult = request.Status == DiscordGrantStatus.Canceled && request.ResultFingerprint is null;
        if (request.Status != DiscordGrantStatus.Queued && !lateCanceledResult && request.ResultFingerprint != outcomeFingerprint)
            return ResultDisposition.Rejected;

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = request.CorrelationId,
            ["MessageId"] = envelope.MessageId, ["RequestId"] = request.Id.ToString() });
        if (lateCanceledResult)
        {
            // A timed-out publication may have reached the broker before the DB recorded it
            // Cancellation stops new sends but cannot prove a Discord role was never granted
            request.RecordLateCanceledResult(outcomeFingerprint, success, now);
            db.AuditLogs.Add(AuditLog.Create(null, "late_canceled_discord_result", "subscription", request.SubscriptionId.ToString()));
        }
        else if (request.Status == DiscordGrantStatus.Queued)
        {
            var status = DiscordGrantStatus.Failed;
            var error = result.ErrorCode;
            if (success)
            {
                var subscription = await db.Subscriptions.SingleAsync(x => x.Id == request.SubscriptionId, ct);
                var canConfirm = subscription.Status == Subscription.SubscriptionStatus.AwaitingAccess && subscription.DurationDays is > 0 ||
                    subscription.Status == Subscription.SubscriptionStatus.Active && subscription.StartAt is not null && subscription.EndAt > now.UtcDateTime;
                if (!canConfirm)
                {
                    status = DiscordGrantStatus.NeedsReview;
                    error = "subscription_state_changed";
                    logger.LogWarning("Discord reports a role grant after subscription state changed; manual reconciliation is required");
                }
                else
                {
                    subscription.ActivateAccess(result.ConfirmedAtUtc!.Value.UtcDateTime);
                    if (!await db.DiscordAccesses.AnyAsync(x => x.SubscriptionId == request.SubscriptionId &&
                        x.DiscordUserId == request.DiscordUserId && x.RoleId == request.RoleId, ct))
                        db.DiscordAccesses.Add(DiscordAccess.Create(request.SubscriptionId, request.DiscordUserId,
                            request.RoleId, request.RoleName, result.ConfirmedAtUtc.Value.UtcDateTime));
                    status = DiscordGrantStatus.Succeeded;
                }
            }
            request.Complete(status, outcomeFingerprint, now, error);
            db.AuditLogs.Add(AuditLog.Create(null, "discord_grant_result", "subscription", request.SubscriptionId.ToString(),
                JsonSerializer.Serialize(new { requestId = request.Id.ToString(), status = status.ToString(), errorCode = error,
                    correlationId = request.CorrelationId })));
        }
        db.IntegrationInbox.Add(InboxReceipt.Create(messageId, requestId, fingerprint, now));
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Discord result committed. Status: {Status}, ErrorCode: {ErrorCode}", request.Status, request.ErrorCode);
        return ResultDisposition.Applied;
    }

    private static string Hash<T>(T value)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, BotMessages.Json)));
}
