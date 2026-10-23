using backend.Auth;
using backend.Features.RoadMaps.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageRoadMaps)]
[Route("roadmaps")]
public sealed class RoadMapsController(RoadMapAdministrationService service) : ApiControllerBase
{
    [HttpGet("templates")]
    public async Task<ActionResult<PagedResponse<RoadMapTemplateListItemResponse>>> GetTemplates([FromQuery] RoadMapTemplateListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetTemplates(query, cancellationToken, Actor));

    [HttpGet("templates/{id:int}")]
    public async Task<ActionResult<RoadMapTemplateDetailResponse>> GetTemplateById(int id, CancellationToken cancellationToken)
        => Map(await service.GetTemplateById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPost("templates")]
    public async Task<ActionResult<RoadMapTemplateDetailResponse>> CreateTemplate([FromBody] CreateRoadMapTemplateRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateTemplate(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetTemplateById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPatch("templates/{id:int}")]
    public async Task<ActionResult<RoadMapTemplateDetailResponse>> PatchTemplate(int id,
        [FromBody] PatchRoadMapTemplateRequest request,
        CancellationToken cancellationToken)
        => Map(await service.PatchTemplate(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPost("templates/{id:int}/status")]
    public async Task<ActionResult<RoadMapTemplateDetailResponse>> ChangeTemplateStatus(int id,
        [FromBody] ChangeRoadMapTemplateStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeTemplateStatus(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPost("templates/{id:int}/items")]
    public async Task<ActionResult<RoadMapTemplateDetailResponse>> AddItem(int id,
        [FromBody] CreateRoadMapItemRequest request,
        CancellationToken cancellationToken)
        => Map(await service.AddItem(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPatch("items/{id:int}")]
    public async Task<ActionResult<RoadMapItemResponse>> PatchItem(int id,
        [FromBody] PatchRoadMapItemRequest request,
        CancellationToken cancellationToken)
        => Map(await service.PatchItem(id, request, cancellationToken, Actor));

    [HttpGet("assignments")]
    public async Task<ActionResult<PagedResponse<RoadMapAssignmentListItemResponse>>> GetAssignments([FromQuery] RoadMapAssignmentListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetAssignments(query, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPost("assignments")]
    public async Task<ActionResult<RoadMapAssignmentListItemResponse>> CreateAssignment([FromBody] CreateRoadMapAssignmentRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateAssignment(request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageRoadMaps)]
    [HttpPost("assignments/{id}/status")]
    public async Task<ActionResult<RoadMapAssignmentListItemResponse>> ChangeAssignmentStatus(string id,
        [FromBody] ChangeRoadMapAssignmentStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeAssignmentStatus(id, request, cancellationToken, Actor));
}
