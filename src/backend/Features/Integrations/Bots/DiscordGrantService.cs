using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Domain.Auditing;
using backend.Domain.Messaging;
using backend.Domain.Subscriptions;
using backend.Features.Common;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUlid;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Integrations.Bots;

public sealed class DiscordGrantService(ApplicationDbContext db, IOptions<BotMessagingOptions> options,
    TimeProvider clock, ILogger<DiscordGrantService> logger)
{
    internal static bool Snowflake(string? value) => value is not null &&
        Regex.IsMatch(value, "^[1-9][0-9]{16,19}$", RegexOptions.CultureInvariant);

    public async Task<ModuleOutcome<DiscordGrantView>> RequestAsync(Ulid subscriptionId, RequestDiscordGrant body,
        Ulid actorId, string correlationId, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Disabled();
        if (body is null || !Ulid.TryParse(body.RequestId, out var requestId) || requestId == Ulid.Empty || !Snowflake(body.DiscordUserId))
            return Invalid(ToErrorDictionary("body", "A ULID requestId and numeric Discord user ID are required."));
        var existing = await db.DiscordGrantRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (existing is not null)
            return existing.SubscriptionId == subscriptionId && existing.DiscordUserId == body.DiscordUserId
                ? ModuleOutcome<DiscordGrantView>.Success(await ViewAsync(existing, ct))
                : Conflict(new("request_id_conflict", "requestId already identifies another grant."));

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsRelational())
            await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM subscriptions WHERE id = {subscriptionId.ToString()} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);

        var subscription = await db.Subscriptions.Include(x => x.OrderItem).ThenInclude(x => x.Package)
            .SingleOrDefaultAsync(x => x.Id == subscriptionId, ct);
        if (subscription is null) return Missing(new("subscription_not_found", "Subscription was not found."));
        if (subscription.Status != Subscription.SubscriptionStatus.AwaitingAccess ||
            subscription.ProvisioningKey is null || subscription.DurationDays is not > 0 || subscription.Type is not
                (Subscription.SubscriptionType.DiscordStandalone or Subscription.SubscriptionType.DiscordCourseBonus))
            return Conflict(new("discord_grant_not_allowed", "An automatically provisioned Discord subscription awaiting access is required."));
        if (!await db.Payments.AnyAsync(x => x.OrderId == subscription.OrderItem.OrderId && x.Status == "Paid", ct))
            return Conflict(new("paid_order_required", "The purchase must have a successful payment."));
        if (await db.DiscordGrantRequests.AnyAsync(x => x.ActiveSubscriptionId == subscriptionId, ct))
            return Conflict(new("discord_grant_pending", "Another Discord grant is already queued for this subscription."));

        if (!options.Value.DiscordPackages.TryGetValue(subscription.OrderItem.Package.Code, out var target) ||
            !Snowflake(target.GuildId) || !Snowflake(target.RoleId) ||
            string.IsNullOrWhiteSpace(target.RoleName) || target.RoleName.Length > 128)
            return new ModuleFailure(ModuleFailureKind.Unavailable, "discord_mapping_missing", "The package has no valid Discord role mapping.");

        var now = clock.GetUtcNow();
        var request = DiscordGrantRequest.Queue(requestId, subscriptionId, subscription.ClientId, actorId,
            body.DiscordUserId, target.GuildId, target.RoleId, target.RoleName, correlationId, now);
        var envelope = new MessageEnvelope<GrantDiscordRole>(requestId.ToString(), correlationId,
            BotMessages.GrantRequested, 1, now, new(requestId.ToString(), subscriptionId.ToString(),
                subscription.ClientId.ToString(), body.DiscordUserId, target.GuildId, target.RoleId));
        db.DiscordGrantRequests.Add(request);
        db.IntegrationOutbox.Add(OutboxMessage.Create(requestId, correlationId, BotMessages.GrantRequested,
            JsonSerializer.Serialize(envelope, BotMessages.Json), now));
        db.AuditLogs.Add(AuditLog.Create(actorId, "request_discord_access", "subscription", subscriptionId.ToString(),
            JsonSerializer.Serialize(new { requestId = requestId.ToString(), correlationId })));
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Discord grant queued. RequestId: {RequestId}, CorrelationId: {CorrelationId}", requestId, correlationId);
        return ModuleOutcome<DiscordGrantView>.Success(await ViewAsync(request, ct));
    }

    public async Task<ModuleOutcome<DiscordGrantView>> GetAsync(Ulid subscriptionId, Ulid requestId, CancellationToken ct)
    {
        var request = await db.DiscordGrantRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId && x.SubscriptionId == subscriptionId, ct);
        return request is null ? Missing(new("discord_request_not_found", "Discord grant request was not found."))
            : ModuleOutcome<DiscordGrantView>.Success(await ViewAsync(request, ct));
    }

    public async Task<ModuleOutcome<DiscordGrantView>> RetryDeliveryAsync(Ulid subscriptionId, Ulid requestId,
        Ulid actorId, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Disabled();
        var request = await db.DiscordGrantRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.SubscriptionId == subscriptionId, ct);
        if (request is null) return Missing(new("discord_request_not_found", "Discord grant request was not found."));
        if (request.Status != DiscordGrantStatus.Queued)
            return Conflict(new("discord_request_completed", "Completed requests cannot be republished."));
        var outbox = await db.IntegrationOutbox.SingleAsync(x => x.Id == requestId, ct);
        if (outbox.Retry(clock.GetUtcNow()))
        {
            db.AuditLogs.Add(AuditLog.Create(actorId, "retry_discord_delivery", "subscription", subscriptionId.ToString()));
            await db.SaveChangesAsync(ct);
        }
        return ModuleOutcome<DiscordGrantView>.Success(await ViewAsync(request, ct));
    }

    private static ModuleFailure Disabled() => new(ModuleFailureKind.Unavailable,
        "bot_messaging_disabled", "Bot messaging is not enabled for this environment.");

    private async Task<DiscordGrantView> ViewAsync(DiscordGrantRequest request, CancellationToken ct)
    {
        var message = await db.IntegrationOutbox.AsNoTracking().SingleAsync(x => x.Id == request.Id, ct);
        return new(request.Id.ToString(), request.SubscriptionId.ToString(), request.DiscordUserId,
            request.Status.ToString(), message.CanceledAtUtc is not null ? "canceled" : message.PublishedAtUtc is not null ? "published" :
                message.DeadLetteredAtUtc is not null ? "dead_lettered" : "pending", message.Attempts,
            request.ErrorCode ?? message.LastErrorCode, request.RequestedAtUtc, request.CompletedAtUtc);
    }
}
