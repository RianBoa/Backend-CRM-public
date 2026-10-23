using backend.Features.Learning;
using backend.Domain.LearningStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin,OnBoarding")]
[Route("learning")]
public sealed class LearningWorkspaceController(EnrollmentService enrollments, LearningDirectory directory) : ApiControllerBase
{
    [HttpGet("cohorts")]
    public async Task<ActionResult<PagedResponse<CohortCard>>> GetCohorts(
        [FromQuery] LearningDirectoryQuery query, CancellationToken cancellationToken)
    {
        var page = await directory.GetCohortsAsync(query, cancellationToken);
        return Ok(new PagedResponse<CohortCard>(page.Items, page.Total, query.Page, query.PageSize));
    }

    [HttpGet("groups")]
    public async Task<ActionResult<PagedResponse<GroupCard>>> GetGroups(
        [FromQuery] LearningDirectoryQuery query, CancellationToken cancellationToken)
    {
        var page = await directory.GetGroupsAsync(query, cancellationToken);
        return Ok(new PagedResponse<GroupCard>(page.Items, page.Total, query.Page, query.PageSize));
    }

    [HttpGet("assignment-queue")]
    public async Task<ActionResult<PagedResponse<EnrollmentView>>> GetQueue(
        [FromQuery] AssignmentQueueQuery query, CancellationToken cancellationToken)
    {
        var page = await enrollments.GetQueueAsync(query, cancellationToken);
        return Ok(new PagedResponse<EnrollmentView>(page.Items, page.Total, query.Page, query.PageSize));
    }

    [HttpGet("enrollments/{id}")]
    public async Task<ActionResult<EnrollmentView>> GetEnrollment(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var enrollmentId)) return InvalidId("id");
        var enrollment = await enrollments.GetAsync(enrollmentId, cancellationToken);
        return enrollment is null ? NotFound(new ErrorResponse("enrollment_not_found", "Enrollment was not found.")) : Ok(enrollment);
    }

    [HttpPost("enrollments/{id}/assign-group")]
    public async Task<ActionResult<EnrollmentView>> Assign(
        string id, AssignGroupRequest request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var enrollmentId)) return InvalidId("id");
        if (!Ulid.TryParse(request.GroupId, out var groupId)) return InvalidId("groupId");
        return ToAction(await enrollments.AssignAsync(enrollmentId, groupId, CurrentUserId, cancellationToken));
    }

    [HttpPost("enrollments/{id}/status")]
    public async Task<ActionResult<EnrollmentView>> ChangeStatus(
        string id, ChangeLearningStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var enrollmentId)) return InvalidId("id");
        if (!Enum.TryParse<Enrollment.EnrollmentStatus>(request.Status, true, out var status) || !Enum.IsDefined(status))
            return ValidationError(ToErrorDictionary("status", "Unknown learning status."));
        return ToAction(await enrollments.ChangeStatusAsync(enrollmentId, status, CurrentUserId, cancellationToken));
    }

    private ActionResult InvalidId(string field) => ValidationError(ToErrorDictionary(field, "A valid ULID is required."));

    private ActionResult<EnrollmentView> ToAction(LearningChange result) => result.Error switch
    {
        null => Ok(result.Value),
        { Kind: LearningFailure.NotFound } error => NotFound(new ErrorResponse(error.Code, error.Message)),
        var error => Conflict(new ErrorResponse(error.Code, error.Message))
    };
}
