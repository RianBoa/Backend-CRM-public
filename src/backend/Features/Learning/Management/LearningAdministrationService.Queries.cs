using backend.Auth;
using backend.Domain.LearningStructure;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.Learning.Management;

public sealed partial class LearningAdministrationService
{
    public async Task<ModuleOutcome<PagedResponse<CohortListItemResponse>>> GetCohorts(CohortListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var cohortsQuery = dbContext.Cohorts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            cohortsQuery = cohortsQuery.Where(cohort => EF.Functions.ILike(cohort.Name, $"%{search}%"));
        }

        if (query.ProductId is not null)
        {
            cohortsQuery = cohortsQuery.Where(cohort => cohort.ProductId == query.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<Cohort.CohortStatus>(query.Status.Trim(), true, out var status))
        {
            cohortsQuery = cohortsQuery.Where(cohort => cohort.Status == status);
        }

        var total = await cohortsQuery.CountAsync(cancellationToken);

        var items = await cohortsQuery
            .OrderByDescending(cohort => cohort.StartAt)
            .ThenBy(cohort => cohort.Name)
            .ThenBy(cohort => cohort.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cohort => new CohortListItemResponse(
                cohort.Id.ToString(),
                cohort.ProductId,
                cohort.Product != null ? cohort.Product.Name : null,
                cohort.Name,
                cohort.StartAt,
                cohort.EndsAt,
                cohort.Status.ToString(),
                cohort.CreatedAt,
                cohort.Groups.Count,
                cohort.Enrollments.Count))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<CohortListItemResponse>>.Success(new PagedResponse<CohortListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<CohortDetailResponse>> GetCohortById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var cohortId))
        {
            return Invalid(ToErrorDictionary("id", "Cohort id must be a valid ULID."));
        }

        var cohort = await dbContext.Cohorts
            .AsNoTracking()
            .Where(x => x.Id == cohortId)
            .Select(cohort => new CohortDetailResponse(
                cohort.Id.ToString(),
                cohort.ProductId,
                cohort.Product != null ? cohort.Product.Name : null,
                cohort.Name,
                cohort.StartAt,
                cohort.EndsAt,
                cohort.Status.ToString(),
                cohort.CreatedAt,
                cohort.Groups
                    .OrderBy(group => group.Name)
                    .Select(group => new GroupListItemResponse(
                        group.Id.ToString(),
                        group.CohortId.ToString(),
                        group.Name,
                        group.MentorId.HasValue ? group.MentorId.Value.ToString() : null,
                        group.Mentor != null ? group.Mentor.FullName : null,
                        group.PackageId,
                        group.Package != null ? group.Package.Name : null,
                        group.LearningPlatformId,
                        group.CreatedAt,
                        group.Enrollments.Count))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return cohort is null
            ? Missing(new ErrorResponse("cohort_not_found", "Cohort was not found."))
            : ModuleOutcome<CohortDetailResponse>.Success(cohort);
    }

    public async Task<ModuleOutcome<PagedResponse<GroupListItemResponse>>> GetGroups(GroupListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var groupsQuery = dbContext.Groups.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            groupsQuery = groupsQuery.Where(group => EF.Functions.ILike(group.Name, $"%{search}%"));
        }

        if (Ulid.TryParse(query.CohortId?.Trim(), out var cohortId))
        {
            groupsQuery = groupsQuery.Where(group => group.CohortId == cohortId);
        }

        if (query.PackageId is not null)
        {
            groupsQuery = groupsQuery.Where(group => group.PackageId == query.PackageId.Value);
        }

        if (Ulid.TryParse(query.MentorId?.Trim(), out var mentorId))
        {
            groupsQuery = groupsQuery.Where(group => group.MentorId == mentorId);
        }

        var total = await groupsQuery.CountAsync(cancellationToken);

        var items = await groupsQuery
            .OrderBy(group => group.Name)
            .ThenBy(group => group.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(group => new GroupListItemResponse(
                group.Id.ToString(),
                group.CohortId.ToString(),
                group.Name,
                group.MentorId.HasValue ? group.MentorId.Value.ToString() : null,
                group.Mentor != null ? group.Mentor.FullName : null,
                group.PackageId,
                group.Package != null ? group.Package.Name : null,
                group.LearningPlatformId,
                group.CreatedAt,
                group.Enrollments.Count))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<GroupListItemResponse>>.Success(new PagedResponse<GroupListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<PagedResponse<EnrollmentListItemResponse>>> GetEnrollments(EnrollmentListQuery query,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0) return Invalid(errors);

        var page = query.Page is > 0 ? query.Page.Value : DefaultPage;
        var pageSize = query.PageSize is > 0 ? Math.Min(query.PageSize.Value, MaxPageSize) : DefaultPageSize;

        var enrollmentsQuery = dbContext.Enrollments.AsNoTracking().AsQueryable();

        if (Ulid.TryParse(query.OrderId?.Trim(), out var orderId))
        {
            enrollmentsQuery = enrollmentsQuery.Where(enrollment => enrollment.OrderId == orderId);
        }

        if (Ulid.TryParse(query.CohortId?.Trim(), out var cohortId))
        {
            enrollmentsQuery = enrollmentsQuery.Where(enrollment => enrollment.CohortId == cohortId);
        }

        if (Ulid.TryParse(query.GroupId?.Trim(), out var groupId))
        {
            enrollmentsQuery = enrollmentsQuery.Where(enrollment => enrollment.GroupId == groupId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<Enrollment.EnrollmentStatus>(query.Status.Trim(), true, out var status))
        {
            enrollmentsQuery = enrollmentsQuery.Where(enrollment => enrollment.Status == status);
        }

        var total = await enrollmentsQuery.CountAsync(cancellationToken);

        var items = await enrollmentsQuery
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .ThenBy(enrollment => enrollment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(enrollment => new EnrollmentListItemResponse(
                enrollment.Id.ToString(),
                enrollment.OrderId.ToString(),
                enrollment.OrderItemId.HasValue ? enrollment.OrderItemId.Value.ToString() : null,
                enrollment.Order.Client.Name,
                enrollment.CohortId.HasValue ? enrollment.CohortId.Value.ToString() : null,
                enrollment.Cohort != null ? enrollment.Cohort.Name : null,
                enrollment.GroupId.HasValue ? enrollment.GroupId.Value.ToString() : null,
                enrollment.Group != null ? enrollment.Group.Name : null,
                enrollment.Status.ToString(),
                enrollment.IsAddedToChat,
                enrollment.IsAddedToPlatform,
                enrollment.DesiredStartDate,
                enrollment.EnrolledAt))
            .ToListAsync(cancellationToken);

        return ModuleOutcome<PagedResponse<EnrollmentListItemResponse>>.Success(new PagedResponse<EnrollmentListItemResponse>(items, total, page, pageSize));
    }

    public async Task<ModuleOutcome<EnrollmentDetailResponse>> GetEnrollmentById(string id, CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var enrollmentId))
        {
            return Invalid(ToErrorDictionary("id", "Enrollment id must be a valid ULID."));
        }

        var enrollment = await BuildEnrollmentDetail(enrollmentId).FirstOrDefaultAsync(cancellationToken);

        return enrollment is null
            ? Missing(new ErrorResponse("enrollment_not_found", "Enrollment was not found."))
            : ModuleOutcome<EnrollmentDetailResponse>.Success(enrollment);
    }
}
