using NUlid;

namespace backend.Features.Clients.Commands;

public readonly record struct ClientActor(Ulid Id, bool CanManageFinancials);
public enum ClientFailure { Validation, NotFound, Forbidden, Conflict }
public sealed record ClientCommandError(ClientFailure Kind, string Code, string Message,
    Dictionary<string, string[]>? Fields = null);

public sealed record ClientCommandResult
{
    public Ulid? ClientId { get; }
    public ClientCommandError? Error { get; }

    private ClientCommandResult(Ulid? clientId, ClientCommandError? error) => (ClientId, Error) = (clientId, error);

    public static ClientCommandResult Success(Ulid id) => new(id, null);
    public static ClientCommandResult Invalid(Dictionary<string, string[]> fields)
        => new(null, new(ClientFailure.Validation, "validation_failed", "Request is invalid.", fields));
    public static ClientCommandResult Invalid(string field, string message) => Invalid(new() { [field] = [message] });
    public static ClientCommandResult Missing() => new(null, new(ClientFailure.NotFound, "client_not_found", "Client was not found."));
    public static ClientCommandResult Forbidden() => new(null, new(ClientFailure.Forbidden, "forbidden", "Financial write permission is required."));
    public static ClientCommandResult Conflict(string message, string code = "client_conflict")
        => new(null, new(ClientFailure.Conflict, code, message));
}
