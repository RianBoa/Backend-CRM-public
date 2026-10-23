using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Learning.Management;

public sealed record CohortListQuery(int? Page, int? PageSize, string? Search, int? ProductId, string? Status);
public sealed record GroupListQuery(int? Page, int? PageSize, string? Search, string? CohortId, int? PackageId, string? MentorId);
public sealed record EnrollmentListQuery(int? Page, int? PageSize, string? OrderId, string? CohortId, string? GroupId, string? Status);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCohortRequest([Range(1, int.MaxValue)] int ProductId, [StringLength(100), NotBlank] string? Name, [UtcDateTime] DateTime StartAt, [UtcDateTime] DateTime? EndsAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchCohortRequest([Range(1, int.MaxValue)] int? ProductId, [StringLength(100), NotBlank] string? Name, [UtcDateTime] DateTime? StartAt, [UtcDateTime] DateTime? EndsAt)
{
    public bool HasChanges() => ProductId is not null || Name is not null || StartAt is not null || EndsAt is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeCohortStatusRequest([NotBlank] string? Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateGroupRequest(string? CohortId, [StringLength(100), NotBlank] string? Name, string? MentorId, [Range(1, int.MaxValue)] int? PackageId, [StringLength(100)] string? LearningPlatformId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchGroupRequest([StringLength(100), NotBlank] string? Name, string? MentorId, [Range(1, int.MaxValue)] int? PackageId, [StringLength(100)] string? LearningPlatformId)
{
    public bool HasChanges() => Name is not null || MentorId is not null || PackageId is not null || LearningPlatformId is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateEnrollmentRequest(string? OrderId, string? OrderItemId, [UtcDateTime] DateTime? DesiredStartDate);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignEnrollmentGroupRequest(string? GroupId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeEnrollmentStatusRequest([NotBlank] string? Status);

public sealed record CohortListItemResponse(
    string Id,
    int? ProductId,
    string? ProductName,
    string Name,
    DateTime StartAt,
    DateTime? EndsAt,
    string Status,
    DateTime CreatedAt,
    int GroupsCount,
    int EnrollmentsCount);

public sealed record CohortDetailResponse(
    string Id,
    int? ProductId,
    string? ProductName,
    string Name,
    DateTime StartAt,
    DateTime? EndsAt,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<GroupListItemResponse> Groups);

public sealed record GroupListItemResponse(
    string Id,
    string CohortId,
    string Name,
    string? MentorId,
    string? MentorName,
    int? PackageId,
    string? PackageName,
    string? LearningPlatformId,
    DateTime CreatedAt,
    int EnrollmentsCount);

public sealed record EnrollmentListItemResponse(
    string Id,
    string OrderId,
    string? OrderItemId,
    string ClientName,
    string? CohortId,
    string? CohortName,
    string? GroupId,
    string? GroupName,
    string Status,
    bool IsAddedToChat,
    bool? IsAddedToPlatform,
    DateTime? DesiredStartDate,
    DateTime EnrolledAt);

public sealed record EnrollmentDetailResponse(
    string Id,
    string OrderId,
    string? OrderItemId,
    string ClientName,
    string? CohortId,
    string? CohortName,
    string? GroupId,
    string? GroupName,
    string Status,
    bool IsAddedToChat,
    DateTime? AddedToChatAt,
    bool? IsAddedToPlatform,
    DateTime? AddedToPlatformAt,
    DateTime? DesiredStartDate,
    DateTime EnrolledAt);
