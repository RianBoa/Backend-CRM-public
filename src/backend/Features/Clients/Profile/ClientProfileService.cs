using backend.Domain.Auditing;
using backend.Domain.Clients;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;

namespace backend.Features.Clients.Profile;

public sealed class ClientProfileService(ApplicationDbContext db)
{
    public async Task<ClientProfile?> GetAsync(Ulid clientId, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        return client is null ? null : ToProfile(client);
    }

    public async Task<ClientProfile?> ReplaceAsync(
        Ulid clientId, ClientProfile profile, Ulid? actorId, CancellationToken cancellationToken, Ulid? applicationId = null)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        if (client is null) return null;

        client.Rename(profile.Name);
        client.SetAdditionalInfo(new AdditionalInfo(
            profile.Notes, profile.Source, profile.City, profile.BirthDate, Normalize(profile.Occupation))
        {
            PreferredLanguage = Normalize(profile.PreferredLanguage)
        });

        // A single SaveChanges commits both the profile and its audit event atomically.
        db.AuditLogs.Add(AuditLog.Create(actorId, "replace_profile", "client", clientId.ToString(),
            applicationId is null ? null : System.Text.Json.JsonSerializer.Serialize(new { applicationId = applicationId.Value.ToString() })));
        await db.SaveChangesAsync(cancellationToken);
        return ToProfile(client);
    }

    private static ClientProfile ToProfile(Client client) => new(
        client.Name, client.AdditionalInfo?.Notes, client.AdditionalInfo?.PreferredLanguage,
        client.AdditionalInfo?.Source, client.AdditionalInfo?.City,
        client.AdditionalInfo?.BirthDate, client.AdditionalInfo?.Occupation);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
