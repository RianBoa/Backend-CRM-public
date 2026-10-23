using backend.Auth;
using backend.Features.Integrations.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("integrations/sales/orders")]
[Authorize(Policy = SalesIntegrationOptions.Policy)]
[EnableRateLimiting("sales-ingress")]
[RequestSizeLimit(128 * 1024)]
public sealed class SalesOrdersController(SalesOrderImportService importer) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SalesOrderResponse>> Import(SalesOrderRequest request, CancellationToken ct)
    {
        var result = await importer.ImportAsync(request, ct);
        if (result.Error is { } error) return Conflict(new ErrorResponse(error.Code, error.Message));
        return result.Value!.OrderCreated && !result.Value.Replayed
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Ok(result.Value);
    }
}
