using backend.Domain.Clients;
using backend.Features.Clients.Contracts;
using ClientFinancials = backend.Domain.Clients.Financials;

namespace backend.Features.Clients.Commands;

internal static class ClientWriteMapping
{
    internal sealed record NormalizedEmails(
        IReadOnlyList<Email> AllEmails,
        Email? PrimaryEmail,
        IReadOnlyList<Email> SecondaryEmails);

    internal static ContactInfo BuildContacts(ClientContactsRequest? contacts)
        => ContactInfo.Create(
            contacts?.PhoneNumber?.Trim(),
            contacts?.Telegram?.Trim(),
            contacts?.Discord?.Trim(),
            contacts?.Instagram?.Trim());

    internal static ClientFinancials BuildFinancials(ClientFinancialsRequest? financials)
        => new(
            financials?.InitialBudget ?? 0,
            financials?.TotalEarnings ?? 0);

    internal static ContactInfo MergeContacts(ContactInfo current, PatchClientContactsRequest patch)
        => ContactInfo.Create(
            patch.PhoneNumber is not null ? patch.PhoneNumber.Trim() : current.PhoneNumber,
            patch.Telegram is not null ? patch.Telegram.Trim() : current.Telegram,
            patch.Discord is not null ? patch.Discord.Trim() : current.Discord,
            patch.Instagram is not null ? patch.Instagram.Trim() : current.Instagram);

    internal static ClientFinancials MergeFinancials(ClientFinancials current, PatchClientFinancialsRequest patch)
        => new(
            patch.InitialBudget ?? current.InitialBudget,
            patch.TotalEarnings ?? current.TotalEarnings);

    internal static AdditionalInfo? BuildAdditionalInfo(ClientAdditionalInfoRequest? additionalInfo)
    {
        if (additionalInfo is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(additionalInfo.Notes) &&
            string.IsNullOrWhiteSpace(additionalInfo.Source) &&
            string.IsNullOrWhiteSpace(additionalInfo.City) &&
            additionalInfo.BirthDate is null &&
            string.IsNullOrWhiteSpace(additionalInfo.Occupation) &&
            string.IsNullOrWhiteSpace(additionalInfo.PreferredLanguage))
        {
            return null;
        }

        return new AdditionalInfo(
            additionalInfo.Notes,
            additionalInfo.Source,
            additionalInfo.City,
            additionalInfo.BirthDate,
            additionalInfo.Occupation)
        {
            PreferredLanguage = string.IsNullOrWhiteSpace(additionalInfo.PreferredLanguage)
                ? null
                : additionalInfo.PreferredLanguage.Trim()
        };
    }

    internal static AdditionalInfo? MergeAdditionalInfo(AdditionalInfo? current, PatchClientAdditionalInfoRequest patch)
    {
        var currentPreferredLanguage = current?.PreferredLanguage;
        var merged = new AdditionalInfo(
            patch.Notes is not null ? patch.Notes : current?.Notes,
            patch.Source is not null ? patch.Source : current?.Source,
            patch.City is not null ? patch.City : current?.City,
            patch.BirthDate ?? current?.BirthDate,
            patch.Occupation is not null ? patch.Occupation : current?.Occupation)
        {
            PreferredLanguage = patch.PreferredLanguage is not null
                ? (string.IsNullOrWhiteSpace(patch.PreferredLanguage) ? null : patch.PreferredLanguage.Trim())
                : currentPreferredLanguage
        };

        return IsAdditionalInfoEmpty(merged) ? null : merged;
    }

    internal static bool IsAdditionalInfoEmpty(AdditionalInfo info)
        => string.IsNullOrWhiteSpace(info.Notes) &&
           string.IsNullOrWhiteSpace(info.PreferredLanguage) &&
           string.IsNullOrWhiteSpace(info.Source) &&
           string.IsNullOrWhiteSpace(info.City) &&
           info.BirthDate is null &&
           string.IsNullOrWhiteSpace(info.Occupation);

    internal static void SyncEmails(Client client, IReadOnlyList<Email> targetEmails, Email? primaryEmail)
    {
        var currentEmails = client.Emails.Select(email => email.Email.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetEmailValues = targetEmails.Select(email => email.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add replacements first so an email-only client never loses its last contact mid-command.
        foreach (var emailToAdd in targetEmails.Where(email => !currentEmails.Contains(email.Value)))
        {
            client.AddEmail(emailToAdd, isPrimary: false);
        }

        foreach (var emailToRemove in currentEmails.Except(targetEmailValues, StringComparer.OrdinalIgnoreCase).ToList())
        {
            client.RemoveEmail(emailToRemove);
        }

        if (primaryEmail is not null)
        {
            client.SetPrimaryEmail(primaryEmail.Value);
        }
    }

    internal static NormalizedEmails BuildNormalizedEmails(IReadOnlyList<UpsertClientEmailRequest> emails)
    {
        var normalized = emails
            .Select(email => new
            {
                Email = Email.From(email.Email!),
                email.IsPrimary
            })
            .ToList();

        var primaryEmail = normalized.FirstOrDefault(email => email.IsPrimary)?.Email;
        var secondaryEmails = normalized
            .Where(email => !email.IsPrimary)
            .Select(email => email.Email)
            .ToList();

        return new NormalizedEmails(
            normalized.Select(email => email.Email).ToList(),
            primaryEmail,
            secondaryEmails);
    }
}
