using backend.Auth;
using backend.Features.Clients.Contracts;
using backend.Features.Clients.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Route("clients/{id}")]
public sealed class ClientHistoryController(ClientHistoryQueries history) : ApiControllerBase
{
    [HttpGet("subscriptions")]
    public Task<ActionResult<PagedResponse<ClientSubscriptionHistoryResponse>>> Subscriptions(string id, [FromQuery] ClientHistoryQuery query, CancellationToken ct)
        => Read(id, query, false, (clientId) => history.GetSubscriptionsAsync(clientId, query, ct), ct);

    [HttpGet("discord-accesses")]
    public Task<ActionResult<PagedResponse<ClientDiscordAccessResponse>>> Discord(string id, [FromQuery] ClientHistoryQuery query, CancellationToken ct)
        => Read(id, query, false, (clientId) => history.GetDiscordAsync(clientId, query, ct), ct);

    [HttpGet("platform-accesses")]
    public Task<ActionResult<PagedResponse<ClientPlatformAccessResponse>>> Platform(string id, [FromQuery] ClientHistoryQuery query, CancellationToken ct)
        => Read(id, query, false, (clientId) => history.GetPlatformAsync(clientId, query, ct), ct);

    [HttpGet("cases")]
    public Task<ActionResult<PagedResponse<ClientCaseWorkspaceItemResponse>>> Cases(string id, [FromQuery] ClientHistoryQuery query, CancellationToken ct)
        => Read(id, query, true, (clientId) => history.GetCasesAsync(clientId, query,
            User.HasClaim("permission", AuthPolicies.ViewFinancials), ct), ct);

    private async Task<ActionResult<PagedResponse<T>>> Read<T>(string id, ClientHistoryQuery query, bool cases,
        Func<Ulid, Task<PagedResponse<T>>> read, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var clientId)) return ValidationError(ToErrorDictionary("id", "A client ULID is required."));
        var errors = ClientHistoryQueries.Validate(query, cases);
        if (errors.Count > 0) return ValidationError(errors);
        if (!await history.ClientExistsAsync(clientId, ct)) return NotFound(new ErrorResponse("client_not_found", "Client was not found."));
        return Ok(await read(clientId));
    }
}
