using backend.Auth;
using backend.Features.Cases.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageCases)]
[Route("cases")]
public sealed class CasesController(CaseManagementService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CaseListItemResponse>>> GetList([FromQuery] CaseListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("{id}")]
    public async Task<ActionResult<CaseDetailResponse>> GetById(string id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageCases)]
    [HttpPost]
    [Authorize(Policy = AuthPolicies.ManageFinancials)]
    public async Task<ActionResult<CaseDetailResponse>> Create([FromBody] CreateCaseRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageCases)]
    [HttpPost("{id}/status")]
    public async Task<ActionResult<CaseDetailResponse>> ChangeStatus(string id,
        [FromBody] ChangeCaseStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeStatus(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageCases)]
    [HttpPost("{id}/featured")]
    public async Task<ActionResult<CaseDetailResponse>> ChangeFeatured(string id,
        [FromBody] ChangeCaseFeaturedRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeFeatured(id, request, cancellationToken, Actor));
}
