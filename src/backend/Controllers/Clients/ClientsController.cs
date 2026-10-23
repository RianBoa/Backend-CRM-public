using backend.Auth;
using backend.Features.Clients.Commands;
using backend.Features.Clients.Contracts;
using backend.Features.Clients.Directory;
using backend.Features.Clients.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUlid;

namespace backend.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.ManageClients)]
[Route("clients")]
public sealed class ClientsController(
    ClientDirectoryQueries directory, ClientDetailQueries detail, ClientWriteService writes) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ClientListItemResponse>>> GetList(
        [FromQuery] ClientListQuery query, CancellationToken cancellationToken)
    {
        var errors = ClientDirectoryQueries.ValidateQuery(query);
        if (errors.Count > 0) return ValidationError(errors);
        return Ok(await directory.GetListAsync(query, cancellationToken));
    }

    [HttpGet("select")]
    public async Task<ActionResult<SelectResponse<ClientSelectItemResponse>>> GetSelect(
        [FromQuery] string? search, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var validation = new backend.Features.Common.ListValidation().Text(search, "search");
        if (limit is < 1 or > 100) validation.Errors["limit"] = ["limit must be between 1 and 100."];
        if (validation.Errors.Count > 0) return ValidationError(validation.Errors);
        return Ok(await directory.GetSelectAsync(search, limit, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClientDetailResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        var response = await detail.GetAsync(clientId, CanViewFinancials, cancellationToken);
        return response is null ? Missing() : Ok(response);
    }

    [HttpPost]
    public Task<ActionResult<ClientDetailResponse>> Create(
        [FromBody] UpsertClientRequest? request, CancellationToken cancellationToken)
        => ExecuteAsync(actor => writes.CreateAsync(request, actor, cancellationToken), cancellationToken, created: true);

    [HttpPut("{id}")]
    public async Task<ActionResult<ClientDetailResponse>> Update(
        string id, [FromBody] UpsertClientRequest? request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        return await ExecuteAsync(actor => writes.ReplaceAsync(clientId, request, actor, cancellationToken), cancellationToken);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ClientDetailResponse>> Patch(
        string id, [FromBody] PatchClientRequest? request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        return await ExecuteAsync(actor => writes.PatchAsync(clientId, request, actor, cancellationToken), cancellationToken);
    }

    [HttpPost("{id}/primary-email")]
    public async Task<ActionResult<ClientDetailResponse>> SetPrimaryEmail(
        string id, [FromBody] SetPrimaryEmailRequest? request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        return await ExecuteAsync(actor => writes.SetPrimaryEmailAsync(clientId, request, actor, cancellationToken), cancellationToken);
    }

    [HttpPost("{id}/emails")]
    public async Task<ActionResult<ClientDetailResponse>> AddEmail(
        string id, [FromBody] AddClientEmailRequest? request, CancellationToken cancellationToken)
    {
        if (!Ulid.TryParse(id, out var clientId)) return InvalidId();
        return await ExecuteAsync(actor => writes.AddEmailAsync(clientId, request, actor, cancellationToken), cancellationToken);
    }

    private bool CanViewFinancials => User.HasClaim("permission", AuthPolicies.ViewFinancials);

    private async Task<ActionResult<ClientDetailResponse>> ExecuteAsync(
        Func<ClientActor, Task<ClientCommandResult>> command, CancellationToken ct, bool created = false)
    {
        if (CurrentUserId is not { } actorId) return Unauthorized();
        var result = await command(new ClientActor(actorId, User.HasClaim("permission", AuthPolicies.ManageFinancials)));
        if (result.Error is { } error)
        {
            return error.Kind switch
            {
                ClientFailure.Validation => ValidationError(error.Fields ?? ToErrorDictionary("request", error.Message)),
                ClientFailure.Forbidden => StatusCode(403, new ErrorResponse(error.Code, error.Message)),
                ClientFailure.NotFound => NotFound(new ErrorResponse(error.Code, error.Message)),
                _ => Conflict(new ErrorResponse(error.Code, error.Message))
            };
        }

        var response = await detail.GetAsync(result.ClientId!.Value, CanViewFinancials, ct);
        if (response is null) return Missing();
        return created
            ? CreatedAtAction(nameof(GetById), new { id = response.Id }, response)
            : Ok(response);
    }

    private ActionResult InvalidId() => ValidationError(ToErrorDictionary("id", "Client id must be a valid ULID."));
    private NotFoundObjectResult Missing() => NotFound(new ErrorResponse("client_not_found", "Client was not found."));
}
