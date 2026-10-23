using backend.Auth;
using backend.Features.Clients.Purchases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Route("clients/{id}/purchases")]
public sealed class ClientPurchasesController(ClientPurchaseQueries purchases) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PurchaseItem>>> GetList(
        string id, [FromQuery] PurchaseListQuery query, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId("id");
        var page = await purchases.GetListAsync(clientId, query, cancellationToken);
        return page is null
            ? NotFound(new ErrorResponse("client_not_found", "Client was not found."))
            : Ok(page);
    }

    [HttpGet("{orderItemId}")]
    public async Task<ActionResult<PurchaseDetail>> GetDetail(
        string id, string orderItemId, [FromQuery] PurchaseDetailQuery query, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId("id");
        if (!Ulid.TryParse(orderItemId, out var purchaseId)) return InvalidId("orderItemId");
        var detail = await purchases.GetDetailAsync(clientId, purchaseId, query, cancellationToken);
        return detail is null
            ? NotFound(new ErrorResponse("purchase_not_found", "Purchase was not found for this client."))
            : Ok(detail);
    }

    private ActionResult InvalidId(string field)
        => ValidationError(ToErrorDictionary(field, "A valid ULID is required."));
}
