using backend.Auth;
using backend.Domain.Cases;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Cases.Management;

public sealed partial class CaseManagementService
{
    private IQueryable<CaseDetailResponse> BuildDetailQuery(Ulid caseId, bool canViewFinancials)
    {
        return dbContext.Cases
            .AsNoTracking()
            .Where(@case => @case.Id == caseId)
            .Select(@case => new CaseDetailResponse(
                @case.Id.ToString(),
                @case.ClientId.ToString(),
                @case.Client.Name,
                @case.CreatedById.ToString(),
                @case.CreatedBy.FullName,
                @case.Type.ToString(),
                canViewFinancials ? @case.Amount : (decimal?)null,
                @case.Currency,
                @case.ScreenshotUrl,
                @case.TelegramUrl,
                @case.Comment,
                @case.IsFeatured,
                @case.HasConsent,
                @case.Status.ToString(),
                @case.CreatedAt));
    }

    private static IQueryable<Case> ApplySorting(IQueryable<Case> query, string sortBy, string sortDir)
    {
        var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy switch
        {
            "amount" => descending ? query.OrderByDescending(@case => @case.Amount).ThenByDescending(@case => @case.Id) : query.OrderBy(@case => @case.Amount).ThenBy(@case => @case.Id),
            "status" => descending ? query.OrderByDescending(@case => @case.Status).ThenByDescending(@case => @case.Id) : query.OrderBy(@case => @case.Status).ThenBy(@case => @case.Id),
            _ => descending ? query.OrderByDescending(@case => @case.CreatedAt).ThenByDescending(@case => @case.Id) : query.OrderBy(@case => @case.CreatedAt).ThenBy(@case => @case.Id)
        };
    }

    private static string NormalizeSortField(string? sortBy)
        => string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : (AllowedSortFields.FirstOrDefault(field => string.Equals(field, sortBy.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "createdAt");

    private static string NormalizeSortDirection(string? sortDir)
        => string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static bool TryParseUlid(string? value, out Ulid ulid)
        => Ulid.TryParse(value?.Trim(), out ulid);

    private static Dictionary<string, string[]> ValidateCreateCaseRequest(CreateCaseRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            errors["clientId"] = ["Client id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.CreatedById))
        {
            errors["createdById"] = ["CreatedBy id is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Type is required."];
        }

        if (request.Amount < 0)
        {
            errors["amount"] = ["Amount cannot be negative."];
        }

        return errors;
    }
}
