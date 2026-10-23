using System.Text.Json;
using backend.Domain.Auditing;
using backend.Infrastructure;
using NUlid;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Common;

internal static class ModuleAudit
{
    public static async Task SaveGeneratedIdentityAsync(ApplicationDbContext db, Ulid? actorId,
        string action, string entityType, Func<string?> entityId, Func<object?> metadata, CancellationToken ct)
    {
        // Database-generated integer IDs are available only after the first save.
        // Both saves are committed together on relational providers.
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        await SaveAsync(db, actorId, action, entityType, entityId(), metadata(), ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    public static async Task SaveAsync(ApplicationDbContext db, Ulid? actorId, string action,
        string entityType, string? entityId, object? metadata, CancellationToken ct)
    {
        db.AuditLogs.Add(AuditLog.Create(actorId, action, entityType, entityId,
            metadata is null ? null : JsonSerializer.Serialize(metadata)));
        await db.SaveChangesAsync(ct);
    }
}
