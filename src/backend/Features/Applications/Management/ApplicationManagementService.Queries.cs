using backend.Auth;
using backend.Domain.Auditing;
using System.Text.Json;
using backend.Domain.Applications;
using backend.Domain.Users;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Applications.Management;

public sealed partial class ApplicationManagementService
{
    public async Task<ModuleOutcome<PagedResponse<ApplicationListItemResponse>>> GetList(ApplicationListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;
        var sortBy = NormalizeSortField(query.SortBy);
        var sortDir = NormalizeSortDirection(query.SortDir);

        var applicationsQuery = dbContext.Applications
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            applicationsQuery = applicationsQuery.Where(application =>
                EF.Functions.ILike(application.Title, $"%{search}%") ||
                EF.Functions.ILike(application.Description ?? string.Empty, $"%{search}%") ||
                EF.Functions.ILike(application.Author.FullName, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<Application.ApplicationStatus>(query.Status.Trim(), true, out var status))
        {
            applicationsQuery = applicationsQuery.Where(application => application.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Reason) &&
            Enum.TryParse<Application.ApplicationReason>(query.Reason.Trim(), true, out var reason))
        {
            applicationsQuery = applicationsQuery.Where(application => application.Reason == reason);
        }

        if (TryParseUlid(query.TypeId, out var typeId))
        {
            applicationsQuery = applicationsQuery.Where(application => application.TypeId == typeId);
        }

        if (TryParseUlid(query.AuthorId, out var authorId))
        {
            applicationsQuery = applicationsQuery.Where(application => application.AuthorId == authorId);
        }

        if (TryParseUlid(query.ClientId, out var clientId))
        {
            applicationsQuery = applicationsQuery.Where(application => application.ClientId == clientId);
        }

        applicationsQuery = ApplySorting(applicationsQuery, sortBy, sortDir);

        var total = await applicationsQuery.CountAsync(cancellationToken);

        var items = await applicationsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new ApplicationListItemResponse(
                application.Id.ToString(),
                application.TypeId.ToString(),
                application.Type.Name,
                application.AuthorId.ToString(),
                application.Author.FullName,
                application.ClientId.HasValue ? application.ClientId.Value.ToString() : null,
                application.Client != null ? application.Client.Name : null,
                application.OrderId.HasValue ? application.OrderId.Value.ToString() : null,
                application.Title,
                application.Reason.ToString(),
                application.Status.ToString(),
                application.CreatedAt,
                application.ProcessedAt))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<ApplicationListItemResponse>>.Success(new PagedResponse<ApplicationListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<ApplicationDetailResponse>> GetById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!TryParseUlid(id, out var applicationId))
        {
            return Invalid(ToErrorDictionary("id", "Application id must be a valid ULID."));
        }

        var application = await BuildDetailQuery(applicationId).FirstOrDefaultAsync(cancellationToken);

        return application is null
            ? Missing(new ErrorResponse("application_not_found", "Application was not found."))
            : ModuleOutcome<ApplicationDetailResponse>.Success(application);
    }

    public async Task<ModuleOutcome<SelectResponse<ApplicationTypeItemResponse>>> GetTypes(bool? activeOnly,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var query = dbContext.ApplicationTypes.AsNoTracking().AsQueryable();

        if (activeOnly is not false)
        {
            query = query.Where(type => type.IsActive);
        }

        var items = await query
            .OrderBy(type => type.Name)
            .Select(type => new ApplicationTypeItemResponse(
                type.Id.ToString(),
                type.Code,
                type.Name,
                type.TargetRole.ToString(),
                type.SlaDays,
                type.IsActive))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<SelectResponse<ApplicationTypeItemResponse>>.Success(new SelectResponse<ApplicationTypeItemResponse>(items));
    }
}
