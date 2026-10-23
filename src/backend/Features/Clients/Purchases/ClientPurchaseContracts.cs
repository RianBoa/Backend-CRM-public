using System.ComponentModel.DataAnnotations;
using backend.Contracts;

namespace backend.Features.Clients.Purchases;

public sealed record PurchaseListQuery(
    [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(120)] string? Search = null,
    [Range(1, int.MaxValue)] int? ProductId = null,
    [RegularExpression("^(purchaseDate|productName)$")] string SortBy = "purchaseDate",
    [RegularExpression("^(asc|desc)$")] string SortDir = "desc");

public sealed record PurchaseDetailQuery(
    [Range(1, 1000000)] int SubscriptionPage = 1,
    [Range(1, 1000000)] int EnrollmentPage = 1,
    [Range(1, 100)] int PageSize = 20);

public sealed record PurchaseItem(
    string OrderItemId, string OrderId, int ProductId, string ProductCode,
    string ProductName, string ProductType, int PackageId, string PackageName,
    uint Quantity, DateOnly PurchaseDate, string OrderStatus);

public sealed record PurchaseSubscription(
    string Id, string Type, string Status, DateTime? SubscriptionStartAt,
    DateTime? SubscriptionEndAt, int DiscordAccessRecordsCount, int PlatformAccessRecordsCount);

public sealed record PurchaseEnrollment(
    string Id, string Status, string? CohortId, string? CohortName,
    string? GroupId, string? GroupName, DateTime EnrolledAt,
    DateTime? LearningStartAt, DateTime? LearningEndAt);

public sealed record PurchaseDetail(
    PurchaseItem Purchase, PagedResponse<PurchaseSubscription> Subscriptions,
    PagedResponse<PurchaseEnrollment> Enrollments);
