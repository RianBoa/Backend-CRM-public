using backend.Domain.Auditing;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Contacts;

public sealed class ClientContactsService(ApplicationDbContext db)
{
    public async Task<ClientContactsBlock?> GetAsync(Ulid clientId, CancellationToken cancellationToken)
    {
        var contacts = await db.Clients.AsNoTracking().Where(x => x.Id == clientId)
            .Select(x => x.Contacts).FirstOrDefaultAsync(cancellationToken);
        return contacts is null ? null : ClientContactsBlock.From(contacts);
    }

    public async Task<ClientContactsBlock?> ReplaceAsync(
        Ulid clientId, ClientContactsBlock request, Ulid? actorId, CancellationToken cancellationToken)
    {
        // Email is an alternative contact method, so load it before checking the invariant.
        var client = await db.Clients.Include(x => x.Emails)
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        if (client is null) return null;

        var contacts = request.ToContactInfo();
        if (client.Contacts != contacts)
        {
            client.SetContacts(contacts);
            db.AuditLogs.Add(AuditLog.Create(actorId, "replace_contacts", "client", clientId.ToString()));
            await db.SaveChangesAsync(cancellationToken);
        }
        return ClientContactsBlock.From(client.Contacts);
    }
}
