using System.Data;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace backend.Features.Learning;

internal sealed class LearningWriteScope(ApplicationDbContext db, IDbContextTransaction? transaction) : IAsyncDisposable
{
    private bool completed;

    public static async Task<LearningWriteScope> BeginAsync(ApplicationDbContext db, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is { } current && current.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Learning writes require serializable isolation.");
        var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        return new(db, transaction);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (transaction is not null) await transaction.CommitAsync(ct);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction is not null) await transaction.DisposeAsync();
        if (!completed) db.ChangeTracker.Clear();
    }
}
