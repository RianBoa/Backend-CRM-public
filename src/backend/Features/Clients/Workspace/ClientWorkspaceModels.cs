using System.Text.Json;
using backend.Domain.Subscriptions;
using backend.Features.Clients.Contracts;
using NUlid;

namespace backend.Features.Clients.Workspace;

internal sealed record ClientDetailSnapshot(
    string Id,
    string ShortId,
    string Name,
    IReadOnlyList<ClientEmailResponse> Emails,
    ClientContactsResponse Contacts,
    ClientFinancialsResponse? Financials,
    ClientAdditionalInfoResponse AdditionalInfo,
    ClientMetaResponse Meta,
    ClientStatsResponse Stats);

internal sealed record ProductMarker(
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    bool ProductHasDiscordAccess);

internal sealed record OrderedProductSnapshot(
    string OrderId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    string PackageName,
    bool ProductHasDiscordAccess,
    DateOnly OrderCreatedAt,
    string OrderStatus,
    JsonDocument? Meta);

internal sealed record SubscriptionSnapshot(
    string SubscriptionId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    string PackageName,
    bool ProductHasDiscordAccess,
    string Type,
    string Status,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Notes,
    int DiscordAccessesCount,
    int PlatformAccessesCount);

internal sealed record SubscriptionSnapshotDb(
    Ulid SubscriptionId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    string PackageName,
    bool ProductHasDiscordAccess,
    Subscription.SubscriptionType Type,
    Subscription.SubscriptionStatus Status,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Notes,
    int DiscordAccessesCount,
    int PlatformAccessesCount);

internal sealed record PlatformAccessTimelineSnapshot(
    string Id,
    string SubscriptionId,
    int ProductId,
    string ProductName,
    string SubscriptionStatus,
    string PlatformName,
    string PlatformUserId,
    DateTime? GrantedAt);

internal sealed record PlatformAccessTimelineSnapshotDb(
    Ulid Id,
    Ulid SubscriptionId,
    int ProductId,
    string ProductName,
    Subscription.SubscriptionStatus SubscriptionStatus,
    string PlatformName,
    string PlatformUserId,
    DateTime? GrantedAt);

internal sealed record DiscordAccessSnapshotDb(
    Ulid Id,
    Ulid SubscriptionId,
    int ProductId,
    string ProductName,
    Subscription.SubscriptionStatus SubscriptionStatus,
    string DiscordUserId,
    string RoleId,
    string RoleName,
    DateTime? GrantedAt);

internal sealed record ClientCaseWorkspaceItemSnapshotDb(
    Ulid Id,
    backend.Domain.Cases.Case.CaseType Type,
    decimal? Amount,
    string Currency,
    backend.Domain.Cases.Case.CaseStatus Status,
    bool IsFeatured,
    bool HasConsent,
    Ulid CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    string? ScreenshotUrl,
    string? TelegramUrl,
    string? Comment);

internal sealed record LearningProductSnapshotDb(
    int ProductId,
    string PackageName,
    backend.Domain.LearningStructure.Enrollment.EnrollmentStatus Status,
    DateTime? DesiredStartDate,
    DateTime? EnrolledAt,
    string? CohortName,
    DateTime? LearningStartAt,
    DateTime? LearningEndAt,
    string? GroupName,
    Ulid OrderId);

internal sealed record ClientProductAggregate(
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    string? PackageName,
    string Status,
    bool ProductHasDiscordAccess,
    int OrdersCount,
    int SubscriptionsCount,
    int ActiveSubscriptionsCount,
    bool HasDiscordAccess,
    bool HasPlatformAccess,
    DateOnly? PurchaseDate,
    DateTime? AccessStartAt,
    DateTime? AccessEndAt,
    string? CohortName,
    string? GroupName,
    DateTime? LearningStartAt,
    DateTime? LearningEndAt,
    string? OrderComment);

internal sealed record ClientProductContext(
    IReadOnlyList<OrderedProductSnapshot> OrderedProducts,
    IReadOnlyList<SubscriptionSnapshot> Subscriptions,
    IReadOnlyList<ProductMarker> AllProducts,
    IReadOnlyList<ClientProductAggregate> ProductSummaries);

internal sealed record ClientActivityContext(
    IReadOnlyList<ClientDiscordAccessResponse> DiscordAccesses,
    IReadOnlyList<PlatformAccessTimelineSnapshot> PlatformAccesses,
    IReadOnlyList<ClientCaseWorkspaceItemResponse> Cases);
