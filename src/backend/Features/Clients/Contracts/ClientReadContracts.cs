namespace backend.Features.Clients.Contracts;

public sealed record ClientListItemResponse(
    string Id,
    string ShortId,
    string Name,
    string? PrimaryEmail,
    string? PhoneNumber,
    string? Telegram,
    string? Discord,
    DateTimeOffset CreatedAtUtc,
    int ActiveOrdersCount,
    int CompletedOrdersCount,
    IReadOnlyList<string> ProductTags,
    IReadOnlyList<ClientProductResponse> Products);

public sealed record ClientProductResponse(
    int Id,
    string Code,
    string Name);

public sealed record ClientProductListItemResponse(
    int ProductId,
    string ProductName,
    string ProductType,
    string? PackageName,
    string Status);

public sealed record ClientProductDetailResponse(
    int ProductId,
    string ProductCode,
    string ProductName,
    string ProductType,
    string? PackageName,
    string Status,
    DateOnly? PurchaseDate,
    DateTime? AccessStartAt,
    DateTime? AccessEndAt,
    string? CohortName,
    string? GroupName,
    DateTime? LearningStartAt,
    DateTime? LearningEndAt,
    string? OrderComment,
    bool HasDiscordAccess,
    bool HasPlatformAccess,
    int OrdersCount,
    int SubscriptionsCount,
    int ActiveSubscriptionsCount);

public sealed record ClientSelectItemResponse(string Value, string Label, string? Subtitle);

public sealed record ClientDetailResponse(
    string Id,
    string ShortId,
    string Name,
    IReadOnlyList<ClientEmailResponse> Emails,
    ClientContactsResponse Contacts,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    ClientFinancialsResponse? Financials,
    ClientAdditionalInfoResponse AdditionalInfo,
    ClientMetaResponse Meta,
    ClientStatsResponse Stats,
    IReadOnlyList<string> ProductTags,
    ClientProductAccessSummaryResponse ProductAccessSummary,
    IReadOnlyList<ClientProductRelationshipResponse> Products,
    IReadOnlyList<ClientDiscordAccessResponse> DiscordAccesses,
    ClientCasesSummaryResponse CasesSummary,
    IReadOnlyList<ClientCaseWorkspaceItemResponse> Cases,
    IReadOnlyList<ClientProductTimelineItemResponse> ProductTimeline,
    ClientHistoryResponse? History = null);

public sealed record ClientHistoryResponse(int PreviewLimit, int PurchasesTotal, int SubscriptionsTotal,
    int DiscordAccessesTotal, int PlatformAccessesTotal, int CasesTotal,
    string PurchasesUrl, string SubscriptionsUrl, string DiscordAccessesUrl, string PlatformAccessesUrl,
    string CasesUrl, bool ProductTagsTruncated);

public sealed record ClientEmailResponse(string Email, bool IsPrimary);
public sealed record ClientContactsResponse(string? PhoneNumber, string? Telegram, string? Discord, string? Instagram);
public sealed record ClientFinancialsResponse(decimal InitialBudget, decimal TotalEarnings);

public sealed record ClientAdditionalInfoResponse(
    string? Notes,
    string? PreferredLanguage,
    string? Source,
    string? City,
    DateOnly? BirthDate,
    string? Occupation);

public sealed record ClientMetaResponse(DateTimeOffset CreatedAtUtc, string? CreatedBy);

public sealed record ClientStatsResponse(
    int ActiveOrdersCount,
    int CompletedOrdersCount,
    int TotalOrdersCount,
    int TotalSubscriptionsCount,
    int TotalCasesCount);

public sealed record ClientProductAccessSummaryResponse(
    int TotalProductsCount,
    int OrderedProductsCount,
    int SubscribedProductsCount,
    int ActiveSubscriptionsCount,
    int TotalDiscordAccessesCount,
    int ActiveDiscordAccessesCount,
    int TotalPlatformAccessesCount);

public sealed record ClientProductRelationshipResponse(
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
    DateOnly? LatestOrderAt,
    DateTime? LatestSubscriptionStartAt,
    DateTime? LatestSubscriptionEndAt,
    string? CohortName,
    string? GroupName,
    DateTime? LearningStartAt,
    DateTime? LearningEndAt,
    string? OrderComment);

public sealed record ClientDiscordAccessResponse(
    string Id,
    string SubscriptionId,
    int ProductId,
    string ProductName,
    string SubscriptionStatus,
    string DiscordUserId,
    string RoleId,
    string RoleName,
    DateTime? GrantedAt);

public sealed record ClientCasesSummaryResponse(
    int TotalCasesCount,
    int FeaturedCasesCount,
    int ApprovedCasesCount,
    int PublishedCasesCount,
    DateTime? LatestCaseCreatedAt);

public sealed record ClientCaseWorkspaceItemResponse(
    string Id,
    string Type,
    decimal? Amount,
    string Currency,
    string Status,
    bool IsFeatured,
    bool HasConsent,
    string CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    string? ScreenshotUrl,
    string? TelegramUrl,
    string? Comment);

public sealed record ClientProductTimelineItemResponse(
    string Type,
    DateTime OccurredAt,
    string Title,
    string? Subtitle,
    string RelatedId,
    int? ProductId,
    string? ProductName,
    string? Status);
