using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Subscriptions.Management;

public sealed record SubscriptionListQuery(
    int? Page,
    int? PageSize,
    string? SortBy,
    string? SortDir,
    string? ClientId,
    int? ProductId,
    string? Status,
    string? Type,
    DateTime? ActiveOn);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSubscriptionRequest(
    string? ClientId,
    [Range(1, int.MaxValue)] int ProductId,
    string? OrderItemId,
    [UtcDateTime] DateTime StartAt,
    [UtcDateTime] DateTime EndAt,
    [NotBlank] string? Type,
    [NotBlank] string? Status,
    [StringLength(1000)] string? Notes);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchSubscriptionRequest([UtcDateTime] DateTime? StartAt, [UtcDateTime] DateTime? EndAt, [StringLength(1000)] string? Notes)
{
    public bool HasChanges() => StartAt is not null || EndAt is not null || Notes is not null;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeSubscriptionStatusRequest([NotBlank] string? Status);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddDiscordAccessRequest([StringLength(64)] string? DiscordUserId, [StringLength(64)] string? RoleId, [StringLength(128)] string? RoleName, [UtcDateTime] DateTime? GrantedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPlatformAccessRequest([StringLength(100)] string? PlatformName, [StringLength(100)] string? PlatformUserId, [UtcDateTime] DateTime? GrantedAt, [StringLength(1000)] string? Notes);

public sealed record SubscriptionListItemResponse(
    string Id,
    string ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string Type,
    string Status,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Notes,
    int DiscordAccessesCount,
    int PlatformAccessesCount);

public sealed record SubscriptionDetailResponse(
    string Id,
    string ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string OrderItemId,
    string Type,
    string Status,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Notes,
    IReadOnlyList<DiscordAccessResponse> DiscordAccesses,
    IReadOnlyList<PlatformAccessResponse> PlatformAccesses,
    IReadOnlyList<SubscriptionAdjustmentResponse> Adjustments);

public sealed record DiscordAccessResponse(string Id, string DiscordUserId, string RoleId, string RoleName, DateTime? GrantedAt);
public sealed record PlatformAccessResponse(string Id, string PlatformName, string PlatformUserId, DateTime? GrantedAt, string? Notes);

public sealed record SubscriptionAdjustmentResponse(
    string Id,
    string Type,
    DateTime AdjustmentDate,
    int? DaysChanged,
    DateTime? OldEndDate,
    DateTime? NewEndDate,
    string? NewRole,
    string? Reason,
    string Source,
    string? RelatedOrderId,
    string? CreatedBy,
    DateTime CreatedAt);
