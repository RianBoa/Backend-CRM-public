using backend.Auth;
using backend.Features.Clients.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Route("clients/{id}/profile")]
public sealed class ClientProfileController(ClientProfileService profiles) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientProfile>> Get(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId))
            return ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));

        var profile = await profiles.GetAsync(clientId, cancellationToken);
        return profile is null
            ? NotFound(new ErrorResponse("client_not_found", "Client was not found."))
            : Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<ClientProfile>> Replace(
        string id, ClientProfile request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId))
            return ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));

        var profile = await profiles.ReplaceAsync(clientId, request, CurrentUserId, cancellationToken);
        return profile is null
            ? NotFound(new ErrorResponse("client_not_found", "Client was not found."))
            : Ok(profile);
    }
}
