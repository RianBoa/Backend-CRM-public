using System.ComponentModel.DataAnnotations;
using backend.Domain.Auditing;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Subscriptions;

public sealed record ActivateAccessRequest([Required] DateTimeOffset? GrantedAt);
public sealed record SubscriptionAccessView(string Id, string Status, int? DurationDays,
    DateTime? StartAt, DateTime? EndAt);
public sealed record AccessActivation(SubscriptionAccessView? Value, string? ErrorCode, string? Message);

public sealed class AccessActivationService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<AccessActivation> ResumeAsync(Ulid id, Ulid? actorId, CancellationToken ct)
    {
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (subscription is null) return new(null, "subscription_not_found", "Subscription was not found.");
        bool changed;
        try { changed = subscription.ResumeAccess(clock.GetUtcNow().UtcDateTime); }
        catch (InvalidOperationException ex) { return new(null, "resume_conflict", ex.Message); }
        if (changed)
        {
            db.AuditLogs.Add(AuditLog.Create(actorId, "resume_access", "subscription", id.ToString()));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                return new(null, "resume_conflict", "Subscription changed concurrently. Reload and retry.");
            }
        }
        return new(new(subscription.Id.ToString(), subscription.Status.ToString(), subscription.DurationDays,
            subscription.StartAt, subscription.EndAt), null, null);
    }

    public async Task<AccessActivation> ActivateAsync(
        Ulid id, DateTimeOffset grantedAt, Ulid? actorId, CancellationToken cancellationToken)
    {
        if (grantedAt > clock.GetUtcNow())
            return new(null, "future_grant", "Actual access cannot be granted in the future.");
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (subscription is null) return new(null, "subscription_not_found", "Subscription was not found.");
        bool changed;
        try { changed = subscription.ActivateAccess(grantedAt.UtcDateTime); }
        catch (InvalidOperationException ex) { return new(null, "activation_conflict", ex.Message); }
        catch (ArgumentException ex) { return new(null, "activation_conflict", ex.Message); }
        if (changed)
        {
            db.AuditLogs.Add(AuditLog.Create(actorId, "activate_access", "subscription", id.ToString()));
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return new(null, "activation_conflict", "Subscription changed concurrently. Reload and retry.");
            }
        }
        return new(new(subscription.Id.ToString(), subscription.Status.ToString(), subscription.DurationDays,
            subscription.StartAt, subscription.EndAt), null, null);
    }
}
