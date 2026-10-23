using backend.Domain.Auditing;
using backend.Domain.Clients;
using backend.Features.Clients.Contracts;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using static backend.Features.Clients.Commands.ClientWriteMapping;

namespace backend.Features.Clients.Commands;

public sealed class ClientWriteService(ApplicationDbContext db)
{
    public async Task<ClientCommandResult> CreateAsync(UpsertClientRequest? request, ClientActor actor, CancellationToken ct)
    {
        if (request?.Financials is not null && !actor.CanManageFinancials) return ClientCommandResult.Forbidden();
        var errors = ClientWriteValidation.ValidateUpsertRequest(request);
        if (errors.Count > 0) return ClientCommandResult.Invalid(errors);
        return await ExecuteAsync("create", actor, () =>
        {
            var emails = BuildNormalizedEmails(request!.Emails ?? []);
            var contacts = BuildContacts(request.Contacts);
            // Establish the primary email before validating an otherwise empty contact block.
            var client = Client.Create(request.Name!.Trim(), emails.PrimaryEmail,
                emails.PrimaryEmail is null ? contacts : null, BuildFinancials(request.Financials),
                BuildAdditionalInfo(request.AdditionalInfo), createdBy: actor.Id.ToString());
            if (emails.PrimaryEmail is not null) client.SetContacts(contacts);
            foreach (var email in emails.SecondaryEmails) client.AddEmail(email, false);
            db.Clients.Add(client);
            return client;
        }, ct);
    }

    public async Task<ClientCommandResult> ReplaceAsync(Ulid id, UpsertClientRequest? request, ClientActor actor, CancellationToken ct)
    {
        if (request?.Financials is not null && !actor.CanManageFinancials) return ClientCommandResult.Forbidden();
        var errors = ClientWriteValidation.ValidateUpsertRequest(request);
        if (errors.Count > 0) return ClientCommandResult.Invalid(errors);
        var client = await LoadAsync(id, ct);
        if (client is null) return ClientCommandResult.Missing();
        return await ExecuteAsync("update", actor, () =>
        {
            var emails = BuildNormalizedEmails(request!.Emails ?? []);
            var contacts = BuildContacts(request.Contacts);
            var info = BuildAdditionalInfo(request.AdditionalInfo);
            var financials = request.Financials is null ? client.Financials : BuildFinancials(request.Financials);
            client.Rename(request.Name!.Trim());
            var hasNonEmailContact = contacts.PhoneNumber is not null || contacts.Telegram is not null || contacts.Discord is not null;
            if (hasNonEmailContact) client.SetContacts(contacts);
            SyncEmails(client, emails.AllEmails, emails.PrimaryEmail);
            client.SetContacts(contacts);
            client.SetFinancials(financials);
            client.SetAdditionalInfo(info);
            return client;
        }, ct);
    }

    public async Task<ClientCommandResult> PatchAsync(Ulid id, PatchClientRequest? request, ClientActor actor, CancellationToken ct)
    {
        if (request is null) return ClientCommandResult.Invalid("body", "Request body is required.");
        if (request.Financials is not null && !actor.CanManageFinancials) return ClientCommandResult.Forbidden();
        if (!request.HasChanges()) return ClientCommandResult.Invalid("body", "At least one field must be provided for patch.");
        var errors = ClientWriteValidation.ValidatePatchRequest(request);
        if (errors.Count > 0) return ClientCommandResult.Invalid(errors);
        var client = await LoadAsync(id, ct);
        if (client is null) return ClientCommandResult.Missing();
        return await ExecuteAsync("patch", actor, () =>
        {
            var contacts = request.Contacts is null ? client.Contacts : MergeContacts(client.Contacts, request.Contacts);
            var financials = request.Financials is null ? client.Financials : MergeFinancials(client.Financials, request.Financials);
            var info = request.AdditionalInfo is null ? client.AdditionalInfo : MergeAdditionalInfo(client.AdditionalInfo, request.AdditionalInfo);
            if (request.Name is not null) client.Rename(request.Name.Trim());
            client.SetContacts(contacts);
            client.SetFinancials(financials);
            client.SetAdditionalInfo(info);
            return client;
        }, ct);
    }

    public async Task<ClientCommandResult> SetPrimaryEmailAsync(Ulid id, SetPrimaryEmailRequest? request, ClientActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Email)) return ClientCommandResult.Invalid("email", "Email is required.");
        var client = await LoadAsync(id, ct);
        if (client is null) return ClientCommandResult.Missing();
        return await ExecuteAsync("set_primary_email", actor, () => { client.SetPrimaryEmail(request.Email); return client; }, ct, "email");
    }

    public async Task<ClientCommandResult> AddEmailAsync(Ulid id, AddClientEmailRequest? request, ClientActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Email)) return ClientCommandResult.Invalid("email", "Email is required.");
        var client = await LoadAsync(id, ct);
        if (client is null) return ClientCommandResult.Missing();
        Email email;
        try { email = Email.From(request.Email); }
        catch (ArgumentException error) { return ClientCommandResult.Invalid("email", error.Message); }
        if (client.Emails.Any(x => x.Email.Value == email.Value))
            return ClientCommandResult.Conflict("Client already has this email.", "email_already_exists");
        return await ExecuteAsync("add_email", actor, () => { client.AddEmail(email, request.IsPrimary); return client; }, ct, "email");
    }

    private Task<Client?> LoadAsync(Ulid id, CancellationToken ct)
        => db.Clients.Include(x => x.Emails).FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task<ClientCommandResult> ExecuteAsync(
        string action, ClientActor actor, Func<Client> change, CancellationToken ct, string errorField = "request")
    {
        Client client;
        try { client = change(); }
        catch (ArgumentException error) { return ClientCommandResult.Invalid(errorField, error.Message); }
        catch (InvalidOperationException error) { return ClientCommandResult.Conflict(error.Message); }

        db.AuditLogs.Add(AuditLog.Create(actor.Id, action, "client", client.Id.ToString()));
        // Infrastructure failures must reach the global handler, not become domain conflicts.
        await db.SaveChangesAsync(ct);
        return ClientCommandResult.Success(client.Id);
    }
}
