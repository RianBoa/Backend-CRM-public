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
    private async Task<CohortDetailResponse> BuildCohortDetail(Ulid cohortId, CancellationToken cancellationToken)
        => await dbContext.Cohorts
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
            .FirstAsync(cancellationToken);

    private async Task<GroupListItemResponse> BuildGroupListItem(Ulid groupId, CancellationToken cancellationToken)
        => await dbContext.Groups
            .AsNoTracking()
            .Where(group => group.Id == groupId)
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
            .FirstAsync(cancellationToken);

    private IQueryable<EnrollmentDetailResponse> BuildEnrollmentDetail(Ulid enrollmentId)
        => dbContext.Enrollments
            .AsNoTracking()
            .Where(x => x.Id == enrollmentId)
            .Select(enrollment => new EnrollmentDetailResponse(
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
                enrollment.AddedToChatAt,
                enrollment.IsAddedToPlatform,
                enrollment.AddedToPlatformAt,
                enrollment.DesiredStartDate,
                enrollment.EnrolledAt));

    private static Dictionary<string, string[]> ValidateCreateCohortRequest(CreateCohortRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (request.ProductId <= 0)
        {
            errors["productId"] = ["ProductId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (request.EndsAt is null || request.EndsAt <= request.StartAt)
        {
            errors["endsAt"] = ["EndsAt must be greater than StartAt."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateCreateGroupRequest(CreateGroupRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.CohortId))
        {
            errors["cohortId"] = ["CohortId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        return errors;
    }
}
