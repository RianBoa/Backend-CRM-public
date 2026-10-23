using backend.Domain.Auditing;
using backend.Domain.Messaging;
using backend.Features.Common;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Subscriptions;

public sealed class SubscriptionCancellationService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<ModuleOutcome<SubscriptionAccessView>> CancelPendingAsync(Ulid id, Ulid? actorId, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsRelational())
            await db.Subscriptions.FromSqlInterpolated($"SELECT * FROM subscriptions WHERE id = {id.ToString()} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (subscription is null) return Missing(new("subscription_not_found", "Subscription was not found."));
        bool changed;
        try { changed = subscription.CancelPending(); }
        catch (InvalidOperationException ex) { return Conflict(new("cancel_pending_conflict", ex.Message)); }
        if (changed)
        {
            var requests = await db.DiscordGrantRequests.Where(x => x.SubscriptionId == id && x.Status == DiscordGrantStatus.Queued).ToListAsync(ct);
            foreach (var request in requests)
            {
                // Share the dispatcher's row lock before deciding whether delivery can still be stopped
                var rows = db.Database.IsRelational()
                    ? await db.IntegrationOutbox.FromSqlInterpolated($"SELECT * FROM integration_outbox WHERE \"Id\" = {request.Id.ToString()} FOR UPDATE").ToListAsync(ct)
                    : await db.IntegrationOutbox.Where(x => x.Id == request.Id).ToListAsync(ct);
                if (rows.Single().CancelUnpublished(clock.GetUtcNow())) request.CancelBeforeDispatch(clock.GetUtcNow());
            }
            db.AuditLogs.Add(AuditLog.Create(actorId, "cancel_pending_access", "subscription", id.ToString()));
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ModuleOutcome<SubscriptionAccessView>.Success(new(subscription.Id.ToString(), subscription.Status.ToString(),
            subscription.DurationDays, subscription.StartAt, subscription.EndAt));
    }
}
