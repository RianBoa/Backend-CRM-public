using backend.Auth;
using backend.Domain.Clients;
using backend.Features.Clients.Contacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Route("clients/{id}/contacts")]
public sealed class ClientContactsController(ClientContactsService contacts) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientContactsBlock>> Get(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId))
            return ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));

        var value = await contacts.GetAsync(clientId, cancellationToken);
        return value is null ? ClientNotFound() : Ok(value);
    }

    [HttpPut]
    public async Task<ActionResult<ClientContactsBlock>> Replace(
        string id, ClientContactsBlock request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId))
            return ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));

        try
        {
            var value = await contacts.ReplaceAsync(clientId, request, CurrentUserId, cancellationToken);
            return value is null ? ClientNotFound() : Ok(value);
        }
        catch (ContactMethodRequiredException ex)
        {
            return Conflict(new ErrorResponse("last_contact_required", ex.Message));
        }
    }

    private NotFoundObjectResult ClientNotFound()
        => NotFound(new ErrorResponse("client_not_found", "Client was not found."));
}
