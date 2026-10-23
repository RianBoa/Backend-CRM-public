using backend.Domain.Clients;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Integrations.Sales;

internal sealed record SalesClientResolution(Client? Client, bool Created, bool Changed, SalesImportError? Error);

public sealed class SalesClientResolver(ApplicationDbContext db)
{
    public const string Provider = "pipedrive";

    internal async Task<SalesClientResolution> ResolveAsync(SalesClient incoming, CancellationToken ct)
    {
        var email = Email.From(incoming.Email);
        // The migration canonicalizes providers; equality uses the composite identity index.
        var personIds = await db.ExternalAccounts
            .Where(x => x.Provider == Provider && x.ExternalId == incoming.PipedrivePersonId)
            .Select(x => x.ClientId).Distinct().Take(2).ToListAsync(ct);
        var emailIds = await db.ClientEmails.Where(x => x.Email.Value == email.Value)
            .Select(x => x.ClientId).Distinct().Take(2).ToListAsync(ct);
        if (personIds.Count > 1 || emailIds.Count > 1)
            return Conflict("client_identity_ambiguous", "Person ID or email matches multiple clients; manual review is required.");
        if (personIds.Count == 1 && emailIds.Count == 1 && personIds[0] != emailIds[0])
            return Conflict("client_identity_conflict", "Person ID and email belong to different clients.");

        var id = personIds.Count == 1 ? personIds[0] : emailIds.Count == 1 ? emailIds[0] : (NUlid.Ulid?)null;
        Client client;
        var created = id is null;
        var changed = created;
        if (id is { } clientId)
        {
            client = await db.Clients.Include(x => x.Emails).SingleAsync(x => x.Id == clientId, ct);
            var linkedPersonIds = await db.ExternalAccounts
                .Where(x => x.ClientId == clientId && x.Provider == Provider)
                .Select(x => x.ExternalId).Distinct().Take(2).ToListAsync(ct);
            if (linkedPersonIds.Any(x => x != incoming.PipedrivePersonId))
                return Conflict("client_person_conflict", "The email's client is already linked to another Pipedrive person.");
            // Preserve staff-edited name, notes, financials and the current primary email.
            changed = !client.Emails.Any(x => x.Email.Value == email.Value);
            client.AddEmail(email, isPrimary: false);
        }
        else
        {
            client = Client.Create(incoming.Name.Trim(), email, financials: new Financials(0, 0), createdBy: "integration:sales");
            client.SetContacts(ContactInfo.Create());
            db.Clients.Add(client);
        }
        if (personIds.Count == 0)
            db.ExternalAccounts.Add(ExternalAccount.Create(client.Id, Provider, incoming.PipedrivePersonId));
        return new(client, created, changed || personIds.Count == 0, null);
    }

    private static SalesClientResolution Conflict(string code, string message) => new(null, false, false, new(code, message));
}
