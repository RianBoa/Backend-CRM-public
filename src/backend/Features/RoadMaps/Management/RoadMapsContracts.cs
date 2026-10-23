using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.RoadMaps.Management;

public sealed record RoadMapTemplateListQuery(int? Page, int? PageSize, string? Search, int? ProductId, bool? IsActive);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRoadMapTemplateRequest([StringLength(200), NotBlank] string? Name, [Range(1, int.MaxValue)] int ProductId, [StringLength(1000)] string? Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchRoadMapTemplateRequest([StringLength(200), NotBlank] string? Name, [StringLength(1000)] string? Description)
{
    public bool HasChanges() => Name is not null || Description is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeRoadMapTemplateStatusRequest(bool? IsActive);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRoadMapItemRequest([StringLength(200), NotBlank] string? Title, [NotBlank] string? Type, int DaysFromStart);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchRoadMapItemRequest([StringLength(200), NotBlank] string? Title, [NotBlank] string? Type, int? DaysFromStart)
{
    public bool HasChanges() => Title is not null || Type is not null || DaysFromStart is not null;
}

public sealed record RoadMapAssignmentListQuery(int? Page, int? PageSize, string? EnrollmentId, string? UserId, string? Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateRoadMapAssignmentRequest([Range(1, int.MaxValue)] int RoadMapItemId, string? EnrollmentId, int DueAtDaysFromEnrollment, string? UserId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeRoadMapAssignmentStatusRequest([NotBlank] string? Status);
public sealed record RoadMapTemplateListItemResponse(int Id, string Name, string? Description, int ProductId, string ProductName, bool IsActive, int ItemsCount);
public sealed record RoadMapTemplateDetailResponse(int Id, string Name, string? Description, int ProductId, string ProductName, bool IsActive, IReadOnlyList<RoadMapItemResponse> Items);
public sealed record RoadMapItemResponse(int Id, string Title, string Type, int DaysFromStart);

public sealed record RoadMapAssignmentListItemResponse(
    string Id,
    int RoadMapItemId,
    string RoadMapItemTitle,
    string EnrollmentId,
    string ClientName,
    string UserId,
    string AssignedByName,
    string Status,
    int DueAtDaysFromEnrollment,
    DateTime AssignedAt);
