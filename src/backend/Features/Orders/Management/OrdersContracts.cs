using backend.Domain.Orders;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Features.Common;
namespace backend.Features.Orders.Management;

public sealed record OrderListQuery(
    int? Page,
    int? PageSize,
    string? SortBy,
    string? SortDir,
    string? ClientId,
    string? Status,
    string? Source,
    int? ProductId,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest(
    string? ClientId,
    string? Source,
    [Required, MinLength(1), MaxLength(100)] List<CreateOrderItemRequest>? Items,
    [MaxLength(100)] List<AddPaymentRequest>? Payments);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderItemRequest([Range(1, int.MaxValue)] int ProductId, [Range(1, int.MaxValue)] int PackageId, [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Price, [Range(typeof(uint), "1", "100000")] uint Quantity);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPaymentRequest(
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount,
    [StringLength(10), NotBlank] string? Currency,
    [NotBlank] string? Status,
    int? TotalInstallments,
    int? CurrentInstallment,
    decimal? AmountUA,
    decimal? ExchangeRate,
    [StringLength(100)] string? Provider,
    [StringLength(100)] string? Invoice,
    [UtcDateTime] DateTime? PaidAt,
    [UtcDateTime] DateTime? PaidUntil);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatchOrderRequest([NotBlank] string? Status, string? Source)
{
    public bool HasChanges() => Status is not null || Source is not null;
}

internal sealed record OrderListItemBase(
    string Id,
    string ClientId,
    string ClientName,
    OrderStatus Status,
    OrderSource Source,
    DateOnly CreatedAt,
    int ItemsCount,
    decimal TotalAmount);

public sealed record OrderListItemResponse(
    string Id,
    string ClientId,
    string ClientName,
    string Status,
    string Source,
    DateOnly CreatedAt,
    int ItemsCount,
    decimal TotalAmount,
    IReadOnlyList<string> ProductTags);

public sealed record OrderDetailResponse(
    string Id,
    string ClientId,
    string ClientName,
    string Status,
    string Source,
    DateOnly CreatedAt,
    IReadOnlyList<OrderItemResponse> Items,
    IReadOnlyList<PaymentResponse> Payments);

public sealed record OrderItemResponse(
    string Id,
    int ProductId,
    string ProductName,
    int PackageId,
    string PackageName,
    decimal Price,
    uint Quantity);

public sealed record PaymentResponse(
    string Id,
    decimal Amount,
    string Currency,
    string Status,
    string? Provider,
    string? Invoice,
    int TotalInstallments,
    int? CurrentInstallment,
    decimal? AmountUA,
    decimal? ExchangeRate,
    DateTime? PaidAt,
    DateTime? PaidUntil);
