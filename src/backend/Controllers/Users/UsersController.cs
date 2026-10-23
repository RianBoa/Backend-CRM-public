using backend.Auth;
using backend.Features.Users.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageUsers)]
[Route("users")]
public sealed class UsersController(TeamAdministrationService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetList([FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("select")]
    public async Task<ActionResult<SelectResponse<UserSelectItemResponse>>> GetSelect([FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? activeOnly,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Map(await service.GetSelect(search, role, activeOnly, limit, cancellationToken, Actor));

    [HttpGet("roles")]
    public ActionResult<SelectResponse<RoleSelectItemResponse>> GetRoles()
        => Map(service.GetRoles(Actor));

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailResponse>> GetById(string id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<UserDetailResponse>> Create([FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [HttpPatch("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDetailResponse>> Patch(string id,
        [FromBody] PatchUserRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Patch(id, request, cancellationToken, Actor));

    [HttpPost("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDetailResponse>> ChangeStatus(string id,
        [FromBody] ChangeUserStatusRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ChangeStatus(id, request, cancellationToken, Actor));

    [HttpPost("{id}/password")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDetailResponse>> ResetPassword(string id,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
        => Map(await service.ResetPassword(id, request, cancellationToken, Actor));
}
