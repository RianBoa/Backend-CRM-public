using backend.Auth;
using backend.Features.RoadMaps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageRoadMaps)]
[Route("learning/enrollments/{enrollmentId}/roadmap")]
public sealed class StudentRoadMapController(StudentRoadMapService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<StudentRoadMapItem>>> Get(
        string enrollmentId, [FromQuery] StudentRoadMapQuery query, CancellationToken ct)
    {
        if (!Ulid.TryParse(enrollmentId, out var id)) return InvalidId();
        var response = await service.GetAsync(id, query, ct);
        return response is null ? NotFound(new ErrorResponse("enrollment_not_found", "Enrollment was not found.")) : Ok(response);
    }

    [HttpPut("{assignmentId}/completion")]
    public async Task<ActionResult<StudentRoadMapItem>> SetCompletion(
        string enrollmentId, string assignmentId, CompletionRequest request, CancellationToken ct)
    {
        if (!Ulid.TryParse(enrollmentId, out var enrollment) || !Ulid.TryParse(assignmentId, out var assignment)) return InvalidId();
        if (CurrentUserId is not { } actor) return Unauthorized();
        var result = await service.SetCompletionAsync(enrollment, assignment, actor, request.Completed!.Value, ct);
        return result.ErrorCode switch
        {
            null => Ok(result.Item),
            "actor_forbidden" => StatusCode(403, new ErrorResponse(result.ErrorCode, "Active user is required.")),
            "roadmap_assignment_not_found" => NotFound(new ErrorResponse(result.ErrorCode, "Assignment was not found in this enrollment.")),
            var code => Conflict(new ErrorResponse(code, "Refresh the checklist; skipped or concurrently changed assignments cannot be updated."))
        };
    }

    private ActionResult InvalidId() => ValidationError(ToErrorDictionary("id", "Ids must be valid ULIDs."));
}
