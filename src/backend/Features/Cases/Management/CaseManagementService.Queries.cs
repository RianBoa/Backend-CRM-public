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
    public async Task<ModuleOutcome<PagedResponse<CaseListItemResponse>>> GetList(CaseListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var canViewFinancials = actor.CanViewFinancials;
        if (string.Equals(sortBy, "amount", StringComparison.OrdinalIgnoreCase) &&
            !actor.CanViewFinancials) return Denied();
        var sortDir = NormalizeSortDirection(query.SortDir);

        var casesQuery = dbContext.Cases
            .AsNoTracking()
            .AsQueryable();

        if (TryParseUlid(query.ClientId, out var clientId))
        {
            casesQuery = casesQuery.Where(@case => @case.ClientId == clientId);
        }

        if (TryParseUlid(query.CreatedById, out var createdById))
        {
            casesQuery = casesQuery.Where(@case => @case.CreatedById == createdById);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<Case.CaseStatus>(query.Status.Trim(), true, out var status))
        {
            casesQuery = casesQuery.Where(@case => @case.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type) &&
            Enum.TryParse<Case.CaseType>(query.Type.Trim(), true, out var type))
        {
            casesQuery = casesQuery.Where(@case => @case.Type == type);
        }

        if (query.IsFeatured is not null)
        {
            casesQuery = casesQuery.Where(@case => @case.IsFeatured == query.IsFeatured.Value);
        }

        if (query.HasConsent is not null)
        {
            casesQuery = casesQuery.Where(@case => @case.HasConsent == query.HasConsent.Value);
        }

        if (query.CreatedFrom is not null)
        {
            casesQuery = casesQuery.Where(@case => @case.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo is not null)
        {
            casesQuery = casesQuery.Where(@case => @case.CreatedAt <= query.CreatedTo.Value);
        }

        casesQuery = ApplySorting(casesQuery, sortBy, sortDir);

        var total = await casesQuery.CountAsync(cancellationToken);

        var items = await casesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(@case => new CaseListItemResponse(
                @case.Id.ToString(),
                @case.ClientId.ToString(),
                @case.Client.Name,
                @case.CreatedById.ToString(),
                @case.CreatedBy.FullName,
                @case.Type.ToString(),
                canViewFinancials ? @case.Amount : (decimal?)null,
                @case.Currency,
                @case.Status.ToString(),
                @case.IsFeatured,
                @case.HasConsent,
                @case.CreatedAt))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<CaseListItemResponse>>.Success(new PagedResponse<CaseListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<CaseDetailResponse>> GetById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var caseId))
        {
            return Invalid(ToErrorDictionary("id", "Case id must be a valid ULID."));
        }

        var @case = await BuildDetailQuery(caseId, actor.CanViewFinancials).FirstOrDefaultAsync(cancellationToken);

        return @case is null
            ? Missing(new ErrorResponse("case_not_found", "Case was not found."))
            : ModuleOutcome<CaseDetailResponse>.Success(@case);
    }
}
