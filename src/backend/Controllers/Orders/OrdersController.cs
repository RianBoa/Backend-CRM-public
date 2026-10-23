using backend.Auth;
using backend.Features.Orders.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageOrders)]
[Authorize(Policy = AuthPolicies.ViewFinancials)]
[Route("orders")]
public sealed class OrdersController(OrderManagementService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderListItemResponse>>> GetList([FromQuery] OrderListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDetailResponse>> GetById(string id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageOrders)]
    [HttpPost]
    [Authorize(Policy = AuthPolicies.ManageFinancials)]
    public async Task<ActionResult<OrderDetailResponse>> Create([FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageOrders)]
    [HttpPatch("{id}")]
    [Authorize(Policy = AuthPolicies.ManageFinancials)]
    public async Task<ActionResult<OrderDetailResponse>> Patch(string id,
        [FromBody] PatchOrderRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Patch(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageOrders)]
    [HttpPost("{id}/payments")]
    [Authorize(Policy = AuthPolicies.ManageFinancials)]
    public async Task<ActionResult<OrderDetailResponse>> AddPayment(string id,
        [FromBody] AddPaymentRequest request,
        CancellationToken cancellationToken)
        => Map(await service.AddPayment(id, request, cancellationToken, Actor));
}
