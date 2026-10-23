using backend.Auth;
using backend.Domain.RoadMaps;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.RoadMaps.Management;

public sealed partial class RoadMapAdministrationService
{
    public async Task<ModuleOutcome<PagedResponse<RoadMapTemplateListItemResponse>>> GetTemplates(RoadMapTemplateListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var templatesQuery = dbContext.RoadMapTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            templatesQuery = templatesQuery.Where(template =>
                EF.Functions.ILike(template.Name, $"%{search}%") ||
                EF.Functions.ILike(template.Description ?? string.Empty, $"%{search}%"));
        }

        if (query.ProductId is not null)
        {
            templatesQuery = templatesQuery.Where(template => template.ProductId == query.ProductId.Value);
        }

        if (query.IsActive is not null)
        {
            templatesQuery = templatesQuery.Where(template => template.IsActive == query.IsActive.Value);
        }

        var total = await templatesQuery.CountAsync(cancellationToken);

        var items = await templatesQuery
            .OrderBy(template => template.Name)
            .ThenBy(template => template.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(template => new RoadMapTemplateListItemResponse(
                template.Id,
                template.Name,
                template.Description,
                template.ProductId,
                template.Product.Name,
                template.IsActive,
                dbContext.RoadMapItems.Count(item => item.RoadMapTemplateId == template.Id)))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<RoadMapTemplateListItemResponse>>.Success(new PagedResponse<RoadMapTemplateListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<RoadMapTemplateDetailResponse>> GetTemplateById(int id, CancellationToken cancellationToken, ModuleActor actor)
    {
        var template = await BuildTemplateDetail(id).FirstOrDefaultAsync(cancellationToken);
        return template is null
            ? Missing(new ErrorResponse("roadmap_template_not_found", "Roadmap template was not found."))
            : ModuleOutcome<RoadMapTemplateDetailResponse>.Success(template);
    }

    public async Task<ModuleOutcome<PagedResponse<RoadMapAssignmentListItemResponse>>> GetAssignments(RoadMapAssignmentListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var assignmentsQuery = dbContext.RoadMapAssignments.AsNoTracking().AsQueryable();

        if (Ulid.TryParse(query.EnrollmentId?.Trim(), out var enrollmentId))
        {
            assignmentsQuery = assignmentsQuery.Where(assignment => assignment.EnrollmentId == enrollmentId);
        }

        if (Ulid.TryParse(query.UserId?.Trim(), out var userId))
        {
            assignmentsQuery = assignmentsQuery.Where(assignment => assignment.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<RoadMapAssignment.AssignmentStatus>(query.Status.Trim(), true, out var status))
        {
            assignmentsQuery = assignmentsQuery.Where(assignment => assignment.Status == status);
        }

        var total = await assignmentsQuery.CountAsync(cancellationToken);

        var items = await assignmentsQuery
            .OrderByDescending(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(assignment => new RoadMapAssignmentListItemResponse(
                assignment.Id.ToString(),
                assignment.RoadMapItemId,
                assignment.RoadMapItem.Title,
                assignment.EnrollmentId.ToString(),
                assignment.Enrollment.Order.Client.Name,
                assignment.UserId.ToString(),
                assignment.AssignedBy.FullName,
                assignment.Status.ToString(),
                assignment.DueAtDaysFromEnrollment,
                assignment.AssignedAt))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<RoadMapAssignmentListItemResponse>>.Success(new PagedResponse<RoadMapAssignmentListItemResponse>(items, total, page, pageSize));
    }
}
