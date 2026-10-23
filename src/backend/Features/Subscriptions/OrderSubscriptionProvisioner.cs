using backend.Domain.Auditing;
using backend.Domain.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUlid;

namespace backend.Features.Subscriptions;

public sealed class OrderSubscriptionProvisioner(ApplicationDbContext db)
{
    public async Task StageForOrderAsync(Ulid orderId, Ulid? actorId, CancellationToken cancellationToken)
    {
        var hasPaidPayment = db.Payments.Local.Any(x => x.OrderId == orderId && x.Status == "Paid") ||
            await db.Payments.AnyAsync(x => x.OrderId == orderId && x.Status == "Paid", cancellationToken);
        if (hasPaidPayment)
        {
            var persisted = await db.OrderItems.Where(x => x.OrderId == orderId)
                .Include(x => x.Order).Include(x => x.Product).Include(x => x.Package)
                .ToListAsync(cancellationToken);
            var purchases = persisted.Concat(db.OrderItems.Local.Where(x => x.OrderId == orderId))
                .DistinctBy(x => x.Id).ToList();
            var ids = purchases.Select(x => x.Id).ToList();
            var existing = await db.Subscriptions.Where(x => ids.Contains(x.OrderItemId))
                .Select(x => new { x.OrderItemId, x.Type }).ToListAsync(cancellationToken);
            var provisioned = existing.Select(x => (x.OrderItemId, x.Type)).ToHashSet();
            provisioned.UnionWith(db.Subscriptions.Local.Select(x => (x.OrderItemId, x.Type)));

            foreach (var purchase in purchases)
            foreach (var subscription in SubscriptionEligibility.ForPurchase(purchase))
            {
                if (!provisioned.Add((subscription.OrderItemId, subscription.Type))) continue;
                db.Subscriptions.Add(subscription);
                db.AuditLogs.Add(AuditLog.Create(actorId, "await_access", "subscription", subscription.Id.ToString()));
            }
        }
    }

    public async Task<bool> SaveOrderChangesAsync(Ulid orderId, Ulid? actorId, CancellationToken cancellationToken)
    {
        await StageForOrderAsync(orderId, actorId, cancellationToken);
        try
        {
            // Payment/order changes and provisioned subscriptions commit as one unit.
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: "23505", ConstraintName: "ux_subscription_provisioning_key" })
        {
            return false;
        }
    }
}
