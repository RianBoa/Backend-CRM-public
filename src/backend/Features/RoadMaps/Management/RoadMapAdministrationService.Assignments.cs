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
    public async Task<ModuleOutcome<RoadMapAssignmentListItemResponse>> CreateAssignment(CreateRoadMapAssignmentRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateAssignmentRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        if (!Ulid.TryParse(request.EnrollmentId, out var enrollmentId))
        {
            return Invalid(ToErrorDictionary("enrollmentId", "Enrollment id must be a valid ULID."));
        }

        if (!Ulid.TryParse(request.UserId, out var userId))
        {
            return Invalid(ToErrorDictionary("userId", "User id must be a valid ULID."));
        }
        if (actor.Id != userId) return Denied();

        var item = await dbContext.RoadMapItems.Include(x => x.RoadMapTemplate)
            .FirstOrDefaultAsync(x => x.Id == request.RoadMapItemId, cancellationToken);
        if (item is null)
        {
            return Missing(new ErrorResponse("roadmap_item_not_found", "Roadmap item was not found."));
        }

        var enrollment = await dbContext.Enrollments
            .Include(x => x.OrderItem)
            .Include(x => x.Order)
            .ThenInclude(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == enrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return Missing(new ErrorResponse("enrollment_not_found", "Enrollment was not found."));
        }

        if (enrollment.OrderItem is null)
            return Conflict(new ErrorResponse("enrollment_purchase_required", "Link the enrollment to its purchase before assigning a roadmap."));
        if (item.RoadMapTemplate.ProductId != enrollment.OrderItem.ProductId)
            return Conflict(new ErrorResponse("roadmap_product_mismatch", "Roadmap template does not match the purchased product."));

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Missing(new ErrorResponse("user_not_found", "User was not found."));
        }

        try
        {
            var assignment = RoadMapAssignment.Create(
                request.RoadMapItemId,
                item,
                enrollmentId,
                enrollment,
                request.DueAtDaysFromEnrollment,
                userId,
                user);

            dbContext.RoadMapAssignments.Add(assignment);
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "roadmap_assignment", assignment.Id.ToString(), new { request.EnrollmentId, request.UserId, request.RoadMapItemId }, cancellationToken);

            return ModuleOutcome<RoadMapAssignmentListItemResponse>.Success(new RoadMapAssignmentListItemResponse(
                assignment.Id.ToString(),
                assignment.RoadMapItemId,
                item.Title,
                assignment.EnrollmentId.ToString(),
                enrollment.Order.Client.Name,
                assignment.UserId.ToString(),
                user.FullName,
                assignment.Status.ToString(),
                assignment.DueAtDaysFromEnrollment,
                assignment.AssignedAt));
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<RoadMapAssignmentListItemResponse>> ChangeAssignmentStatus(string id,
        ChangeRoadMapAssignmentStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var assignmentId))
        {
            return Invalid(ToErrorDictionary("id", "Assignment id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<RoadMapAssignment.AssignmentStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var assignment = await dbContext.RoadMapAssignments
            .Include(x => x.RoadMapItem)
            .Include(x => x.Enrollment)
            .ThenInclude(x => x.Order)
            .ThenInclude(x => x.Client)
            .Include(x => x.AssignedBy)
            .FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Missing(new ErrorResponse("roadmap_assignment_not_found", "Roadmap assignment was not found."));
        }

        assignment.UpdateStatus(status);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_status", "roadmap_assignment", assignment.Id.ToString(), new { request.Status }, cancellationToken);

        return ModuleOutcome<RoadMapAssignmentListItemResponse>.Success(new RoadMapAssignmentListItemResponse(
            assignment.Id.ToString(),
            assignment.RoadMapItemId,
            assignment.RoadMapItem.Title,
            assignment.EnrollmentId.ToString(),
            assignment.Enrollment.Order.Client.Name,
            assignment.UserId.ToString(),
            assignment.AssignedBy.FullName,
            assignment.Status.ToString(),
            assignment.DueAtDaysFromEnrollment,
            assignment.AssignedAt));
    }
}
