using backend.Auth;
using backend.Features.Learning.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageLearningStructure)]
[Route("learning-structure")]
public sealed class LearningStructureController(LearningAdministrationService service) : ApiControllerBase
{
    [HttpGet("cohorts")]
    public async Task<ActionResult<PagedResponse<CohortListItemResponse>>> GetCohorts([FromQuery] CohortListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetCohorts(query, cancellationToken, Actor));

    [HttpGet("cohorts/{id}")]
    public async Task<ActionResult<CohortDetailResponse>> GetCohortById(string id, CancellationToken cancellationToken)
        => Map(await service.GetCohortById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("cohorts")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<CohortDetailResponse>> CreateCohort([FromBody] CreateCohortRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateCohort(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetCohortById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPatch("cohorts/{id}")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<CohortDetailResponse>> PatchCohort(string id,
        [FromBody] PatchCohortRequest request,
        CancellationToken cancellationToken)
        => Map(await service.PatchCohort(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("cohorts/{id}/status")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<CohortDetailResponse>> ChangeCohortStatus(string id,
        [FromBody] ChangeCohortStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeCohortStatus(id, request, cancellationToken, Actor));

    [HttpGet("groups")]
    public async Task<ActionResult<PagedResponse<GroupListItemResponse>>> GetGroups([FromQuery] GroupListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetGroups(query, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("groups")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<GroupListItemResponse>> CreateGroup([FromBody] CreateGroupRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateGroup(request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPatch("groups/{id}")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<GroupListItemResponse>> PatchGroup(string id,
        [FromBody] PatchGroupRequest request,
        CancellationToken cancellationToken)
        => Map(await service.PatchGroup(id, request, cancellationToken, Actor));

    [HttpGet("enrollments")]
    public async Task<ActionResult<PagedResponse<EnrollmentListItemResponse>>> GetEnrollments([FromQuery] EnrollmentListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetEnrollments(query, cancellationToken, Actor));

    [HttpGet("enrollments/{id}")]
    public async Task<ActionResult<EnrollmentDetailResponse>> GetEnrollmentById(string id, CancellationToken cancellationToken)
        => Map(await service.GetEnrollmentById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("enrollments")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<EnrollmentDetailResponse>> CreateEnrollment([FromBody] CreateEnrollmentRequest request,
        CancellationToken cancellationToken)
        => Map(await service.CreateEnrollment(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetEnrollmentById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("enrollments/{id}/assign-group")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<EnrollmentDetailResponse>> AssignEnrollmentToGroup(string id,
        [FromBody] AssignEnrollmentGroupRequest request,
        [FromServices] backend.Features.Learning.EnrollmentService enrollmentService,
        CancellationToken cancellationToken)
        => Map(await service.AssignEnrollmentToGroup(id, request, enrollmentService, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageLearningStructure)]
    [HttpPost("enrollments/{id}/status")]
    [Authorize(Roles = "Admin,OnBoarding")]
    public async Task<ActionResult<EnrollmentDetailResponse>> ChangeEnrollmentStatus(string id,
        [FromBody] ChangeEnrollmentStatusRequest request,
        [FromServices] backend.Features.Learning.EnrollmentService enrollmentService,
        CancellationToken cancellationToken)
        => Map(await service.ChangeEnrollmentStatus(id, request, enrollmentService, cancellationToken, Actor));
}
