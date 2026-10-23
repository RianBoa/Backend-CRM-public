using backend.Features.Clients.Contracts;

namespace backend.Features.Clients.Commands;

internal static class ClientWriteValidation
{
    internal static Dictionary<string, string[]> ValidateUpsertRequest(UpsertClientRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > 120)
        {
            errors["name"] = ["Name must be 120 characters or fewer."];
        }

        var emails = request.Emails ?? [];
        if (emails.Count == 0 &&
            string.IsNullOrWhiteSpace(request.Contacts?.PhoneNumber) &&
            string.IsNullOrWhiteSpace(request.Contacts?.Telegram) &&
            string.IsNullOrWhiteSpace(request.Contacts?.Discord))
        {
            errors["contacts"] = ["Client must have at least one contact method: email, phone, telegram, or discord."];
        }

        var normalizedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var primaryEmailsCount = 0;

        for (var i = 0; i < emails.Count; i++)
        {
            var email = emails[i];
            if (string.IsNullOrWhiteSpace(email.Email))
            {
                errors[$"emails[{i}].email"] = ["Email is required."];
                continue;
            }

            var normalized = email.Email.Trim().ToLowerInvariant();
            if (!normalizedEmails.Add(normalized))
            {
                errors[$"emails[{i}].email"] = ["Duplicate emails are not allowed."];
            }

            if (email.IsPrimary)
            {
                primaryEmailsCount++;
            }
        }

        if (emails.Count > 0 && primaryEmailsCount == 0)
        {
            errors["emails"] = ["One email must be marked as primary when emails are provided."];
        }

        if (primaryEmailsCount > 1)
        {
            errors["emails"] = ["Only one email can be marked as primary."];
        }

        if (request.Financials is not null)
        {
            if (request.Financials.InitialBudget is < 0 or > 9999999999.99m)
            {
                errors["financials.initialBudget"] = ["Initial budget must be between 0 and 9999999999.99."];
            }

            if (request.Financials.TotalEarnings is < 0 or > 9999999999.99m)
            {
                errors["financials.totalEarnings"] = ["Total earnings must be between 0 and 9999999999.99."];
            }
        }

        return errors;
    }
    internal static Dictionary<string, string[]> ValidatePatchRequest(PatchClientRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors["name"] = ["Name cannot be empty."];
            }
            else if (request.Name.Trim().Length > 120)
            {
                errors["name"] = ["Name must be 120 characters or fewer."];
            }
        }

        if (request.Financials is not null)
        {
            if (request.Financials.InitialBudget is < 0 or > 9999999999.99m)
            {
                errors["financials.initialBudget"] = ["Initial budget must be between 0 and 9999999999.99."];
            }

            if (request.Financials.TotalEarnings is < 0 or > 9999999999.99m)
            {
                errors["financials.totalEarnings"] = ["Total earnings must be between 0 and 9999999999.99."];
            }
        }

        return errors;
    }
}
