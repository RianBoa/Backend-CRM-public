using backend.Auth;
using backend.Features.Applications;
using backend.Features.Clients.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;
using TeamRequest = backend.Domain.Applications.Application;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageApplications)]
[Route("applications/{id}")]
public sealed class ApplicationConversationController(ApplicationWorkService work) : ApiControllerBase
{
    [HttpGet("processing")]
    public async Task<ActionResult<ApplicationProcessingView>> GetProcessing(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var applicationId)) return InvalidId();
        var value = await work.GetProcessingAsync(applicationId, cancellationToken);
        return value is null ? Missing() : Ok(value);
    }

    [HttpGet("replies")]
    public async Task<ActionResult<PagedResponse<ReplyView>>> GetReplies(
        string id, [FromQuery] ApplicationPageQuery query, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var applicationId)) return InvalidId();
        var page = await work.GetRepliesAsync(applicationId, query, cancellationToken);
        return page is null ? Missing() : Ok(page);
    }

    [HttpPost("replies")]
    public async Task<ActionResult<ReplyReceipt>> Reply(string id, ReplyRequest request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var applicationId)) return InvalidId();
        if (CurrentUserId is not { } actorId) return Unauthorized();
        TeamRequest.ApplicationStatus? status = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<TeamRequest.ApplicationStatus>(request.Status, true, out var parsed) || !Enum.IsDefined(parsed))
                return ValidationError(ToErrorDictionary("status", "Unknown application status."));
            status = parsed;
        }
        return ToAction(await work.ReplyAsync(applicationId, actorId, request.Body, status, cancellationToken));
    }

    [HttpPut("client-profile")]
    [Authorize(Policy = AuthPolicies.ManageClients)]
    public async Task<ActionResult<ClientProfile>> UpdateClientProfile(
        string id, ClientProfile request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var applicationId)) return InvalidId();
        if (CurrentUserId is not { } actorId) return Unauthorized();
        return ToAction(await work.UpdateClientProfileAsync(applicationId, actorId, request, cancellationToken));
    }

    [HttpGet("activity")]
    public async Task<ActionResult<PagedResponse<ApplicationActivity>>> GetActivity(
        string id, [FromQuery] ApplicationPageQuery query, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var applicationId)) return InvalidId();
        var page = await work.GetActivityAsync(applicationId, query, cancellationToken);
        return page is null ? Missing() : Ok(page);
    }

    private ActionResult InvalidId() => ValidationError(ToErrorDictionary("id", "Application id must be a valid ULID."));
    private NotFoundObjectResult Missing() => NotFound(new ErrorResponse("application_not_found", "Application was not found."));
    private ActionResult<T> ToAction<T>(WorkResult<T> result) => result.Error switch
    {
        null => Ok(result.Value),
        { Kind: WorkFailure.NotFound } error => NotFound(new ErrorResponse(error.Code, error.Message)),
        { Kind: WorkFailure.Forbidden } error => StatusCode(403, new ErrorResponse(error.Code, error.Message)),
        var error => Conflict(new ErrorResponse(error.Code, error.Message))
    };
}
