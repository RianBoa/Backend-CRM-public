using backend.Features.Integrations.Bots;
using backend.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "Admin,OnBoarding")]
[Route("subscriptions/{id}/discord-access-requests")]
public sealed class DiscordGrantController(DiscordGrantService grants) : ApiControllerBase
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<ActionResult<DiscordGrantView>> Request(string id, RequestDiscordGrant request, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var subscriptionId)) return ValidationError(ToErrorDictionary("id", "A subscription ULID is required."));
        if (CurrentUserId is not { } actorId) return Unauthorized();
        var result = await grants.RequestAsync(subscriptionId, request, actorId, RequestLogMiddleware.CorrelationId(HttpContext), ct);
        return Map(result, value => AcceptedAtAction(nameof(Get), new { id, requestId = value.RequestId }, value));
    }

    [HttpGet("{requestId}")]
    public async Task<ActionResult<DiscordGrantView>> Get(string id, string requestId, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var subscriptionId) || !Ulid.TryParse(requestId, out var operationId))
            return ValidationError(ToErrorDictionary("id", "Subscription and request IDs must be ULIDs."));
        return Map(await grants.GetAsync(subscriptionId, operationId, ct));
    }

    [HttpPost("{requestId}/retry-delivery")]
    public async Task<ActionResult<DiscordGrantView>> Retry(string id, string requestId, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var subscriptionId) || !Ulid.TryParse(requestId, out var operationId))
            return ValidationError(ToErrorDictionary("id", "Subscription and request IDs must be ULIDs."));
        if (CurrentUserId is not { } actorId) return Unauthorized();
        return Map(await grants.RetryDeliveryAsync(subscriptionId, operationId, actorId, ct));
    }
}
