using NUlid;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Features.Common;

namespace backend.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ModuleActor Actor => new(CurrentUserId,
        User.HasClaim("permission", backend.Auth.AuthPolicies.ViewFinancials), User.IsInRole("Admin"));

    protected ActionResult<T> Map<T>(ModuleOutcome<T> outcome, Func<T, ActionResult>? created = null)
    {
        if (outcome.Error is { } error)
            return error.Kind switch
            {
                ModuleFailureKind.Validation => ValidationError(error.Errors ?? new()),
                ModuleFailureKind.NotFound => NotFound(new ErrorResponse(error.Code, error.Message)),
                ModuleFailureKind.Conflict => Conflict(new ErrorResponse(error.Code, error.Message)),
                ModuleFailureKind.Forbidden => StatusCode(403, new ErrorResponse(error.Code, error.Message)),
                ModuleFailureKind.Unauthenticated => Unauthorized(new ErrorResponse(error.Code, error.Message)),
                ModuleFailureKind.Unavailable => StatusCode(503, new ErrorResponse(error.Code, error.Message)),
                _ => throw new InvalidOperationException("Unknown application failure.")
            };
        return created is null ? Ok(outcome.Value) : created(outcome.Value!);
    }

    protected ActionResult ValidationError(Dictionary<string, string[]> errors)
        => BadRequest(new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        });

    protected static Dictionary<string, string[]> ToErrorDictionary(string key, string error)
        => new()
        {
            [string.IsNullOrWhiteSpace(key) ? "request" : key] = [error]
        };

    protected Ulid? CurrentUserId
        => Ulid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : null;

}
