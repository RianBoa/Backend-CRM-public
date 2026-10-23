namespace backend.Features.Clients.Contracts;

public sealed record ClientListQuery(
    int? Page,
    int? PageSize,
    string? Search,
    string? SortBy,
    string? SortDir,
    string? Email,
    string? Telegram,
    bool? HasEmail,
    bool? HasTelegram,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo);

public sealed record UpsertClientRequest(
    string? Name,
    List<UpsertClientEmailRequest>? Emails,
    ClientContactsRequest? Contacts,
    ClientFinancialsRequest? Financials,
    ClientAdditionalInfoRequest? AdditionalInfo,
    string? CreatedBy);

public sealed record UpsertClientEmailRequest(string? Email, bool IsPrimary);

public sealed record ClientContactsRequest(
    string? PhoneNumber,
    string? Telegram,
    string? Discord,
    string? Instagram);

public sealed record ClientFinancialsRequest(decimal? InitialBudget, decimal? TotalEarnings);

public sealed record PatchClientRequest(
    string? Name,
    PatchClientContactsRequest? Contacts,
    PatchClientFinancialsRequest? Financials,
    PatchClientAdditionalInfoRequest? AdditionalInfo)
{
    public bool HasChanges()
        => Name is not null ||
           Contacts is not null ||
           Financials is not null ||
           AdditionalInfo is not null;
}

public sealed record ClientAdditionalInfoRequest(
    string? Notes,
    string? PreferredLanguage,
    string? Source,
    string? City,
    DateOnly? BirthDate,
    string? Occupation);

public sealed record PatchClientContactsRequest(
    string? PhoneNumber,
    string? Telegram,
    string? Discord,
    string? Instagram);

public sealed record PatchClientFinancialsRequest(decimal? InitialBudget, decimal? TotalEarnings);

public sealed record PatchClientAdditionalInfoRequest(
    string? Notes,
    string? PreferredLanguage,
    string? Source,
    string? City,
    DateOnly? BirthDate,
    string? Occupation);

public sealed record SetPrimaryEmailRequest(string? Email);
public sealed record AddClientEmailRequest(string? Email, bool IsPrimary);
