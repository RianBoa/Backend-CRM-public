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
    public async Task<ModuleOutcome<EnrollmentDetailResponse>> CreateEnrollment(CreateEnrollmentRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Invalid(ToErrorDictionary("orderId", "Order id is required."));
        }

        if (string.IsNullOrWhiteSpace(request.OrderItemId))
        {
            return Invalid(ToErrorDictionary("orderItemId", "Order item id is required."));
        }

        if (!Ulid.TryParse(request.OrderId, out var orderId))
        {
            return Invalid(ToErrorDictionary("orderId", "Order id must be a valid ULID."));
        }

        if (!Ulid.TryParse(request.OrderItemId, out var orderItemId))
        {
            return Invalid(ToErrorDictionary("orderItemId", "Order item id must be a valid ULID."));
        }

        var orderItem = await dbContext.OrderItems
            .Include(x => x.Order)
            .ThenInclude(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == orderItemId && x.OrderId == orderId, cancellationToken);
        if (orderItem is null)
        {
            return Missing(new ErrorResponse("order_item_not_found", "Order item was not found in this order."));
        }

        var enrollment = Enrollment.Create(orderItem, request.DesiredStartDate);
        dbContext.Enrollments.Add(enrollment);
        await ModuleAudit.SaveAsync(dbContext, actor.Id, "create", "enrollment", enrollment.Id.ToString(), new { request.OrderId, request.OrderItemId }, cancellationToken);

        var response = await BuildEnrollmentDetail(enrollment.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<EnrollmentDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<EnrollmentDetailResponse>> AssignEnrollmentToGroup(string id,
        AssignEnrollmentGroupRequest request,
        backend.Features.Learning.EnrollmentService enrollmentService,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var enrollmentId))
        {
            return Invalid(ToErrorDictionary("id", "Enrollment id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.GroupId))
        {
            return Invalid(ToErrorDictionary("groupId", "Group id is required."));
        }

        if (!Ulid.TryParse(request.GroupId, out var groupId))
        {
            return Invalid(ToErrorDictionary("groupId", "Group id must be a valid ULID."));
        }

        var result = await enrollmentService.AssignAsync(enrollmentId, groupId, actor.Id, cancellationToken);
        if (result.Error is { } error)
            return error.Kind == backend.Features.Learning.LearningFailure.NotFound
                ? Missing(new ErrorResponse(error.Code, error.Message))
                : Conflict(new ErrorResponse(error.Code, error.Message));

        var response = await BuildEnrollmentDetail(enrollmentId).FirstAsync(cancellationToken);
        return ModuleOutcome<EnrollmentDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<EnrollmentDetailResponse>> ChangeEnrollmentStatus(string id,
        ChangeEnrollmentStatusRequest request,
        backend.Features.Learning.EnrollmentService enrollmentService,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (!Ulid.TryParse(id, out var enrollmentId))
        {
            return Invalid(ToErrorDictionary("id", "Enrollment id must be a valid ULID."));
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            return Invalid(ToErrorDictionary("status", "Status is required."));
        }

        if (!Enum.TryParse<Enrollment.EnrollmentStatus>(request.Status.Trim(), true, out var status) || !Enum.IsDefined(status))
        {
            return Invalid(ToErrorDictionary("status", "Status is invalid."));
        }

        var result = await enrollmentService.ChangeStatusAsync(enrollmentId, status, actor.Id, cancellationToken);
        if (result.Error is { } error)
            return error.Kind == backend.Features.Learning.LearningFailure.NotFound
                ? Missing(new ErrorResponse(error.Code, error.Message))
                : Conflict(new ErrorResponse(error.Code, error.Message));

        var response = await BuildEnrollmentDetail(enrollmentId).FirstAsync(cancellationToken);
        return ModuleOutcome<EnrollmentDetailResponse>.Success(response);
    }
}
