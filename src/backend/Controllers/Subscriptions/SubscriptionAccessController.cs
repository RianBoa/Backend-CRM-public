using backend.Features.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin,OnBoarding")]
[Route("subscriptions/{id}")]
public sealed class SubscriptionAccessController(AccessActivationService access) : ApiControllerBase
{
    [HttpPost("activate-access")]
    public async Task<ActionResult<SubscriptionAccessView>> Activate(
        string id, ActivateAccessRequest request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var subscriptionId))
            return ValidationError(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        var result = await access.ActivateAsync(subscriptionId, request.GrantedAt!.Value, CurrentUserId, cancellationToken);
        return result.ErrorCode switch
        {
            null => Ok(result.Value),
            "subscription_not_found" => NotFound(new ErrorResponse(result.ErrorCode, result.Message!)),
            "future_grant" => ValidationError(ToErrorDictionary("grantedAt", result.Message!)),
            _ => Conflict(new ErrorResponse(result.ErrorCode, result.Message!))
        };
    }

    [HttpPost("resume-access")]
    public async Task<ActionResult<SubscriptionAccessView>> Resume(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var subscriptionId))
            return ValidationError(ToErrorDictionary("id", "Subscription id must be a valid ULID."));
        var result = await access.ResumeAsync(subscriptionId, CurrentUserId, cancellationToken);
        return result.ErrorCode switch
        {
            null => Ok(result.Value),
            "subscription_not_found" => NotFound(new ErrorResponse(result.ErrorCode, result.Message!)),
            _ => Conflict(new ErrorResponse(result.ErrorCode, result.Message!))
        };
    }
}
