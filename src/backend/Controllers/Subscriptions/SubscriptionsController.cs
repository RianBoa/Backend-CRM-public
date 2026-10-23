using backend.Auth;
using backend.Features.Subscriptions.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageSubscriptions)]
[Route("subscriptions")]
public sealed class SubscriptionsController(SubscriptionManagementService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SubscriptionListItemResponse>>> GetList([FromQuery] SubscriptionListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("{id}")]
    public async Task<ActionResult<SubscriptionDetailResponse>> GetById(string id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageSubscriptions)]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<SubscriptionDetailResponse>> Create([FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageSubscriptions)]
    [HttpPatch("{id}")]
    public async Task<ActionResult<SubscriptionDetailResponse>> Patch(string id,
        [FromBody] PatchSubscriptionRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Patch(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageSubscriptions)]
    [HttpPost("{id}/status")]
    public async Task<ActionResult<SubscriptionDetailResponse>> ChangeStatus(string id,
        [FromBody] ChangeSubscriptionStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeStatus(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageSubscriptions)]
    [HttpPost("{id}/discord-accesses")]
    public async Task<ActionResult<SubscriptionDetailResponse>> AddDiscordAccess(string id,
        [FromBody] AddDiscordAccessRequest request,
        CancellationToken cancellationToken)
        => Map(await service.AddDiscordAccess(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageSubscriptions)]
    [HttpPost("{id}/platform-accesses")]
    public async Task<ActionResult<SubscriptionDetailResponse>> AddPlatformAccess(string id,
        [FromBody] AddPlatformAccessRequest request,
        CancellationToken cancellationToken)
        => Map(await service.AddPlatformAccess(id, request, cancellationToken, Actor));
}
