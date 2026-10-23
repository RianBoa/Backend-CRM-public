using NUlid;

namespace backend.Features.Common;

public sealed record ModuleActor(Ulid? Id, bool CanViewFinancials, bool CanOverrideSubscriptions = false);

public enum ModuleFailureKind { Validation, NotFound, Conflict, Forbidden, Unauthenticated, Unavailable }

public sealed record ModuleFailure(ModuleFailureKind Kind, string Code, string Message,
    Dictionary<string, string[]>? Errors = null);

public sealed class ModuleOutcome<T>
{
    private ModuleOutcome(T? value, ModuleFailure? error) => (Value, Error) = (value, error);
    public T? Value { get; }
    public ModuleFailure? Error { get; }
    public static ModuleOutcome<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, null);
    }
    public static implicit operator ModuleOutcome<T>(ModuleFailure error)
        => new(default, error ?? throw new ArgumentNullException(nameof(error)));
}

public static class OutcomeErrors
{
    public static ModuleFailure Invalid(Dictionary<string, string[]> errors)
        => new(ModuleFailureKind.Validation, "validation_error", "Request validation failed.", errors);
    public static ModuleFailure Missing(ErrorResponse error)
        => new(ModuleFailureKind.NotFound, error.Code, error.Message);
    public static ModuleFailure Conflict(ErrorResponse error)
        => new(ModuleFailureKind.Conflict, error.Code, error.Message);
    public static ModuleFailure Denied(ErrorResponse? error = null)
        => new(ModuleFailureKind.Forbidden, error?.Code ?? "forbidden", error?.Message ?? "Access is denied.");
    public static ModuleFailure Unauthenticated()
        => new(ModuleFailureKind.Unauthenticated, "unauthorized", "Authentication is required.");
    public static Dictionary<string, string[]> ToErrorDictionary(string key, string message)
        => new() { [string.IsNullOrWhiteSpace(key) ? "request" : key] = [message] };
}
