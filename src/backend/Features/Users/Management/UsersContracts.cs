using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Users.Management;

public sealed record UserListQuery(
    int? Page,
    int? PageSize,
    string? Search,
    string? SortBy,
    string? SortDir,
    string? Role,
    bool? IsActive);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateUserRequest(
    [StringLength(150), NotBlank, EmailAddress] string? Email,
    [StringLength(150), NotBlank] string? FullName,
    [StringLength(100), NotBlank] string? TelegramUsername,
    [StringLength(100)] string? TelegramWork,
    [NotBlank] string? Role,
    [StringLength(128), NotBlank] string? Password,
    bool? IsActive);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchUserRequest(
    [StringLength(150), NotBlank, EmailAddress] string? Email,
    [StringLength(150), NotBlank] string? FullName,
    [StringLength(100), NotBlank] string? TelegramUsername,
    [StringLength(100)] string? TelegramWork,
    [NotBlank] string? Role,
    [StringLength(128), NotBlank] string? Password,
    bool? IsActive)
{
    public bool HasChanges()
        => Email is not null ||
           FullName is not null ||
           TelegramUsername is not null ||
           TelegramWork is not null ||
           Role is not null ||
           Password is not null ||
           IsActive is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeUserStatusRequest(bool? IsActive);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResetUserPasswordRequest([StringLength(128), NotBlank] string? Password);

public sealed record UserListItemResponse(
    string Id,
    string ShortId,
    string FullName,
    string Email,
    string TelegramUsername,
    string? TelegramWork,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    int ApplicationsCount,
    int RoadMapAssignmentsCount);

public sealed record UserSelectItemResponse(string Value, string Label, string Email, string Role, bool IsActive);
public sealed record RoleSelectItemResponse(string Value, string Label);

public sealed record UserDetailResponse(
    string Id,
    string ShortId,
    string FullName,
    string Email,
    string TelegramUsername,
    string? TelegramWork,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    int ApplicationsCount,
    int OpenApplicationsCount,
    int RoadMapAssignmentsCount,
    int PendingRoadMapAssignmentsCount);
