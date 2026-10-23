using backend.Auth;
using backend.Features.Clients.Financials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Authorize(Policy = AuthPolicies.ViewFinancials)]
[Route("clients/{id}/financials")]
public sealed class ClientFinancialsController(ClientFinancialsService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientFinancialsBlock>> Get(string id, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        var result = await service.GetAsync(clientId, ct);
        return result is null ? Missing() : Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = AuthPolicies.ManageFinancials)]
    public async Task<ActionResult<ClientFinancialsBlock>> Replace(string id, ClientFinancialsBlock body, CancellationToken ct)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        if (CurrentUserId is not { } actor) return Unauthorized();
        var result = await service.ReplaceAsync(clientId, actor, body, ct);
        return result is null ? Missing() : Ok(result);
    }

    private ActionResult InvalidId() => ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));
    private NotFoundObjectResult Missing() => NotFound(new ErrorResponse("client_not_found", "Client was not found."));
}
