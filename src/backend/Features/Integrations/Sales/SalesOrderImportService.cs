using System.Data;
using System.Text.Json;
using backend.Domain.Auditing;
using backend.Domain.Integrations;
using backend.Domain.Orders;
using backend.Features.Subscriptions;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Features.Integrations.Sales;

public sealed class SalesOrderImportService(
    ApplicationDbContext db, SalesClientResolver clients, SalesCatalogResolver catalog,
    OrderSubscriptionProvisioner subscriptions, TimeProvider clock)
{
    public async Task<SalesImportResult> ImportAsync(SalesOrderRequest request, CancellationToken ct)
    {
        var eventId = request.EventId.Trim();
        var fingerprint = SalesFingerprint.Event(request);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var receipt = await db.SalesImportReceipts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Source == SalesOrderLink.SourceName && x.EventId == eventId, ct);
            if (receipt is not null)
            {
                if (receipt.Fingerprint != fingerprint)
                    return SalesImportResult.Conflict("event_payload_conflict", "This event ID has already been accepted with different content.");
                var response = JsonSerializer.Deserialize<SalesOrderResponse>(receipt.ResponseJson, SalesFingerprint.Json)
                    ?? throw new InvalidOperationException("Stored sales receipt has no response.");
                return new(response with { Replayed = true }, null);
            }

            var person = await clients.ResolveAsync(request.Client, ct);
            if (person.Error is not null) return new(null, person.Error);
            var client = person.Client!;
            if (person.Changed)
                db.AuditLogs.Add(AuditLog.Create(null, "import_sales_identity", "client", client.Id.ToString(),
                    JsonSerializer.Serialize(new { source = SalesOrderLink.SourceName, eventId,
                        personId = request.Client.PipedrivePersonId, clientCreated = person.Created })));
            var orderLink = await db.SalesOrderLinks.SingleOrDefaultAsync(
                x => x.Source == SalesOrderLink.SourceName && x.DealId == request.Order.PipedriveDealId, ct);
            var orderCreated = orderLink is null;
            Order order;
            if (orderLink is not null)
            {
                order = await db.Orders.SingleAsync(x => x.Id == orderLink.OrderId, ct);
                if (order.ClientId != client.Id)
                    return SalesImportResult.Conflict("deal_client_conflict", "This Pipedrive deal belongs to another client.");
                if (orderLink.ItemsFingerprint != SalesFingerprint.Order(request.Order))
                    return SalesImportResult.Conflict("deal_payload_conflict", "Existing purchase items and currency cannot be overwritten by import.");
            }
            else
            {
                var resolved = await catalog.ResolveAsync(request.Order.Items, ct);
                if (resolved.Error is not null) return new(null, resolved.Error);
                order = Order.Create(client.Id, OrderSource.PipeDrive, DateOnly.FromDateTime(request.Order.PurchasedAt!.Value.UtcDateTime));
                db.Orders.Add(order);
                foreach (var item in resolved.Items!)
                    db.OrderItems.Add(OrderItem.Create(order, item.Product, item.Package, item.Input.Price, checked((uint)item.Input.Quantity)));
                orderLink = SalesOrderLink.Create(request.Order.PipedriveDealId, order.Id,
                    request.Order.Currency.ToUpperInvariant(), SalesFingerprint.Order(request.Order), request.Order.PurchasedAt!.Value);
                db.SalesOrderLinks.Add(orderLink);
            }

            var paymentIds = request.Payments.Select(x => x.ExternalId.Trim()).ToArray();
            var previousPayments = await db.SalesPaymentLinks.Where(x => x.Source == SalesOrderLink.SourceName && paymentIds.Contains(x.ExternalId))
                .ToDictionaryAsync(x => x.ExternalId, ct);
            var paymentsCreated = 0;
            var newPaidAmount = 0m;
            foreach (var incoming in request.Payments)
            {
                var externalId = incoming.ExternalId.Trim();
                var paymentHash = SalesFingerprint.Payment(incoming);
                if (previousPayments.TryGetValue(externalId, out var previous))
                {
                    if (previous.OrderId != order.Id || previous.Fingerprint != paymentHash)
                        return SalesImportResult.Conflict("payment_identity_conflict", "Payment ID was already used with different order or payment data.");
                    continue;
                }
                var payment = Payment.Create(order.Id, incoming.Amount, incoming.Currency.ToUpperInvariant(), "Paid",
                    incoming.TotalInstallments, incoming.CurrentInstallment,
                    provider: SalesFingerprint.Optional(incoming.Provider), invoice: SalesFingerprint.Optional(incoming.Invoice),
                    paidAt: incoming.PaidAt!.Value.UtcDateTime);
                db.Payments.Add(payment);
                db.SalesPaymentLinks.Add(SalesPaymentLink.Create(externalId, payment.Id, order.Id, paymentHash));
                paymentsCreated++;
                newPaidAmount += incoming.Amount;
            }

            if (await db.Payments.AnyAsync(x => x.OrderId == order.Id && x.Currency != orderLink.Currency, ct))
                return SalesImportResult.Conflict("order_currency_conflict", "The order already contains payments in another currency.");
            var alreadyPaid = await db.Payments.Where(x => x.OrderId == order.Id && x.Status == "Paid")
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            if (alreadyPaid + newPaidAmount > request.Order.Items.Sum(x => x.Price * x.Quantity))
                return SalesImportResult.Conflict("payment_total_exceeded", "Confirmed payments cannot exceed the purchase total.");

            // Stage entitlement creation without a separate commit: the receipt is in the same unit of work.
            await subscriptions.StageForOrderAsync(order.Id, null, ct);
            var subscriptionsCreated = db.ChangeTracker.Entries<Domain.Subscriptions.Subscription>()
                .Count(x => x.State == EntityState.Added && x.Entity.OrderItem.OrderId == order.Id);
            var result = new SalesOrderResponse(client.Id.ToString(), order.Id.ToString(), person.Created, orderCreated, paymentsCreated, subscriptionsCreated);
            db.SalesImportReceipts.Add(SalesImportReceipt.Create(eventId, fingerprint,
                JsonSerializer.Serialize(result, SalesFingerprint.Json), clock.GetUtcNow()));
            db.AuditLogs.Add(AuditLog.Create(null, "import_order", "order", order.Id.ToString(),
                JsonSerializer.Serialize(new { source = SalesOrderLink.SourceName, eventId, personId = request.Client.PipedrivePersonId,
                    dealId = request.Order.PipedriveDealId, paymentsCreated, subscriptionsCreated })));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(result, null);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" or "40001" or "40P01" })
        {
            return SalesImportResult.Conflict("import_concurrent_change", "Concurrent import detected. Retry with the same event ID.");
        }
        catch (PostgresException error) when (error.SqlState is "40001" or "40P01")
        {
            return SalesImportResult.Conflict("import_concurrent_change", "Concurrent import detected. Retry with the same event ID.");
        }
        finally
        {
            // Early conflicts must roll back every staged mutation; do not reuse dirty entities for another save.
            db.ChangeTracker.Clear();
        }
    }
}
