using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using backend.Domain.Auditing;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Financials;

public sealed record ClientFinancialsBlock(
    [Required, Range(typeof(decimal), "0", "9999999999.99")] decimal? InitialBudget,
    [Required, Range(typeof(decimal), "0", "9999999999.99")] decimal? TotalEarnings);

public sealed class ClientFinancialsService(ApplicationDbContext db)
{
    public Task<ClientFinancialsBlock?> GetAsync(Ulid clientId, CancellationToken ct)
        => db.Clients.AsNoTracking().Where(x => x.Id == clientId)
            .Select(x => new ClientFinancialsBlock(x.Financials.InitialBudget, x.Financials.TotalEarnings)).FirstOrDefaultAsync(ct);

    public async Task<ClientFinancialsBlock?> ReplaceAsync(Ulid clientId, Ulid actorId, ClientFinancialsBlock body, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId, ct);
        if (client is null) return null;
        var next = new Domain.Clients.Financials(body.InitialBudget!.Value, body.TotalEarnings!.Value);
        if (client.Financials != next)
        {
            var before = client.Financials;
            client.SetFinancials(next);
            db.AuditLogs.Add(AuditLog.Create(actorId, "replace_financials", "client", clientId.ToString(),
                JsonSerializer.Serialize(new { before, after = next })));
            await db.SaveChangesAsync(ct);
        }
        return new(client.Financials.InitialBudget, client.Financials.TotalEarnings);
    }
}
