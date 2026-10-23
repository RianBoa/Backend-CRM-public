using System.ComponentModel.DataAnnotations;

namespace backend.Features.Applications;

public sealed record ApplicationPageQuery(
    [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20);
public sealed record ReplyRequest([Required, StringLength(4000)] string Body, string? Status = null);
public sealed record ReplyView(string Id, string AuthorId, string AuthorName, string Body, DateTime CreatedAt);
public sealed record ApplicationProcessingView(string Id, string Status, string? ClientId,
    string? ProcessedById, DateTime? ProcessedAt, int RepliesCount);
public sealed record ReplyReceipt(ReplyView Reply, ApplicationProcessingView Application);
public sealed record ApplicationActivity(string Id, string? ActorId, string Action, DateTimeOffset CreatedAtUtc);
public enum WorkFailure { NotFound, Forbidden, Conflict }
public sealed record WorkError(WorkFailure Kind, string Code, string Message);
public sealed record WorkResult<T>(T? Value, WorkError? Error)
{
    public static WorkResult<T> Missing() => new(default, new(WorkFailure.NotFound, "application_not_found", "Application or linked client was not found."));
    public static WorkResult<T> Forbidden() => new(default, new(WorkFailure.Forbidden, "inactive_actor", "An active team member is required."));
    public static WorkResult<T> Conflict(string message) => new(default, new(WorkFailure.Conflict, "application_conflict", message));
}
