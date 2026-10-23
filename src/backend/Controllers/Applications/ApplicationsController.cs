using backend.Auth;
using backend.Features.Applications.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageApplications)]
[Route("applications")]
public sealed class ApplicationsController(ApplicationManagementService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ApplicationListItemResponse>>> GetList([FromQuery] ApplicationListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationDetailResponse>> GetById(string id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageApplications)]
    [HttpPost]
    public async Task<ActionResult<ApplicationDetailResponse>> Create([FromBody] CreateApplicationRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageApplications)]
    [HttpPost("{id}/status")]
    public async Task<ActionResult<ApplicationDetailResponse>> ChangeStatus(string id,
        [FromBody] ChangeApplicationStatusRequest request,
        [FromServices] backend.Features.Applications.ApplicationWorkService work,
        CancellationToken cancellationToken)
        => Map(await service.ChangeStatus(id, request, work, cancellationToken, Actor));

    [HttpGet("types")]
    public async Task<ActionResult<SelectResponse<ApplicationTypeItemResponse>>> GetTypes([FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
        => Map(await service.GetTypes(activeOnly, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageApplications)]
    [HttpPost("types")]
    public async Task<ActionResult<ApplicationTypeItemResponse>> CreateType([FromBody] CreateApplicationTypeRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateType(request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageApplications)]
    [HttpPost("types/{id}/status")]
    public async Task<ActionResult<ApplicationTypeItemResponse>> ChangeTypeStatus(string id,
        [FromBody] ChangeApplicationTypeStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeTypeStatus(id, request, cancellationToken, Actor));
}
