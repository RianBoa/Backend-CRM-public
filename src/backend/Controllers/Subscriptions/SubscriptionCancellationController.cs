using backend.Features.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin,OnBoarding")]
[Route("subscriptions/{id}")]
public sealed class SubscriptionCancellationController(SubscriptionCancellationService cancellations) : ApiControllerBase
{
    [HttpPost("cancel-pending")]
    public async Task<ActionResult<SubscriptionAccessView>> CancelPending(string id, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var subscriptionId))
            return ValidationError(ToErrorDictionary("id", "A subscription ULID is required."));
        if (CurrentUserId is not { } actorId) return Unauthorized();
        return Map(await cancellations.CancelPendingAsync(subscriptionId, actorId, ct));
    }
}
