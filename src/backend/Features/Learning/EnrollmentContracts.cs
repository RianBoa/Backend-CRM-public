using System.ComponentModel.DataAnnotations;

namespace backend.Features.Learning;

public sealed record AssignmentQueueQuery(
    [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(120)] string? Search = null,
    [Range(1, int.MaxValue)] int? ProductId = null);

public sealed record EnrollmentView(
    string Id, string ClientId, string ClientName, string OrderId, string? OrderItemId,
    int? ProductId, string? ProductName, string? PackageName,
    string? CohortId, string? CohortName, string? GroupId, string? GroupName,
    string Status, DateTime EnrolledAt, DateTime? LearningStartAt, DateTime? LearningEndAt);

public sealed record AssignGroupRequest([Required] string GroupId);
public sealed record ChangeLearningStatusRequest([Required] string Status);

public sealed record LearningDirectoryQuery(
    [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    bool ActiveOnly = false,
    [StringLength(120)] string? Search = null,
    [Range(1, int.MaxValue)] int? ProductId = null);

public sealed record CohortCard(string Id, string Name, int? ProductId, string Status,
    DateTime StartAt, DateTime? EndAt, int GroupsCount, int StudentsCount);
public sealed record GroupCard(string Id, string Name, string CohortId, string CohortName,
    int? ProductId, int? PackageId, string? PackageName, string CohortStatus, int StudentsCount);

public enum LearningFailure { NotFound, Conflict }
public sealed record LearningError(LearningFailure Kind, string Code, string Message);
public sealed record LearningChange(EnrollmentView? Value, LearningError? Error)
{
    public static LearningChange Missing(string code, string message) => new(null, new(LearningFailure.NotFound, code, message));
    public static LearningChange Conflict(string message) => new(null, new(LearningFailure.Conflict, "enrollment_conflict", message));
}
