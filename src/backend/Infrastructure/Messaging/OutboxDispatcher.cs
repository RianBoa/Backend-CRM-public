using backend.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Messaging;

public interface IBotMessagePublisher
{
    // Completes only after a mandatory publication has been confirmed by the broker.
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}

public sealed class OutboxDispatcher(ApplicationDbContext db, IBotMessagePublisher publisher,
    TimeProvider clock, ILogger<OutboxDispatcher> logger)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // One short transaction leases one row. A crash after publish can cause redelivery, not loss.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.IntegrationOutbox.FromSqlInterpolated($"""
            SELECT * FROM integration_outbox
            WHERE "PublishedAtUtc" IS NULL AND "DeadLetteredAtUtc" IS NULL AND "CanceledAtUtc" IS NULL AND "NextAttemptAtUtc" <= {now}
            ORDER BY "NextAttemptAtUtc", "CreatedAtUtc", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var message = rows.SingleOrDefault();
        if (message is null) return false;
        using var scope = logger.BeginScope(new Dictionary<string, object>
            { ["CorrelationId"] = message.CorrelationId, ["MessageId"] = message.Id.ToString() });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await publisher.PublishAsync(message, timeout.Token);
            message.Published(clock.GetUtcNow());
            logger.LogInformation("Outbox publication confirmed by broker. Attempt: {Attempt}", message.Attempts);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            message.Failed(clock.GetUtcNow());
            logger.LogWarning("Outbox publication failed. ErrorType: {ErrorType}, Attempt: {Attempt}, DeadLettered: {DeadLettered}",
                error.GetType().Name, message.Attempts, message.DeadLetteredAtUtc is not null);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
