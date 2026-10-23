using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Features.Integrations.Sales;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesOrderRequest(
    [Required, StringLength(100, MinimumLength = 1)] string EventId,
    [Required] SalesClient Client,
    [Required] SalesOrder Order,
    [Required, MaxLength(50)] IReadOnlyList<SalesPayment> Payments) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(EventId)) yield return new("Event id is required.", [nameof(EventId)]);
        if (Order?.Items is { } items && items.All(x => x is not null))
        {
            if (items.GroupBy(x => (x.ProductCode?.Trim(), x.PackageCode?.Trim())).Any(x => x.Count() > 1))
                yield return new("Each product/package pair must appear once; use quantity.", ["Order.Items"]);
            if (items.Sum(x => x.Price * x.Quantity) > 9999999999.99m)
                yield return new("Order total exceeds the supported monetary range.", ["Order.Items"]);
        }
        if (Payments is not null && Payments.All(x => x is not null))
        {
            if (Payments.GroupBy(x => x.ExternalId?.Trim()).Any(x => x.Count() > 1))
                yield return new("Payment external IDs must be unique within the event.", [nameof(Payments)]);
            if (Payments.Any(x => x.Currency?.Trim().ToUpperInvariant() != Order?.Currency?.Trim().ToUpperInvariant()))
                yield return new("All payments must use the order currency.", [nameof(Payments)]);
        }
        else if (Payments is not null) yield return new("Payment entries cannot be null.", [nameof(Payments)]);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesClient(
    [Required, RegularExpression("^[1-9][0-9]{0,18}$")] string PipedrivePersonId,
    [Required, StringLength(150)] string Email,
    [Required, StringLength(120)] string Name) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!backend.Domain.Clients.Email.TryFrom(Email, out _))
            yield return new("A valid ASCII email address is required.", [nameof(Email)]);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesOrder(
    [Required, RegularExpression("^[1-9][0-9]{0,18}$")] string PipedriveDealId,
    [Required, RegularExpression("^[A-Za-z]{3,10}$")] string Currency,
    [Required, MinLength(1), MaxLength(50)] IReadOnlyList<SalesOrderItem> Items,
    [Required] DateTimeOffset? PurchasedAt) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Items?.Any(x => x is null) == true) yield return new("Order items cannot be null.", [nameof(Items)]);
        if (PurchasedAt > DateTimeOffset.UtcNow || PurchasedAt < DateTimeOffset.UnixEpoch)
            yield return new("PurchasedAt must be between 1970 and now.", [nameof(PurchasedAt)]);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesOrderItem(
    [Required, StringLength(64)] string ProductCode,
    [Required, StringLength(64)] string PackageCode,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Price,
    [Range(1, 100000)] int Quantity) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Price != decimal.Round(Price, 2)) yield return new("Price supports at most two decimal places.", [nameof(Price)]);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesPayment(
    [Required, StringLength(100)] string ExternalId,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount,
    [Required, RegularExpression("^[A-Za-z]{3,10}$")] string Currency,
    [Required, RegularExpression("^Paid$")] string Status,
    [Required] DateTimeOffset? PaidAt,
    [StringLength(100)] string? Provider = null,
    [StringLength(100)] string? Invoice = null,
    [Range(1, 120)] int TotalInstallments = 1,
    [Range(1, 120)] int? CurrentInstallment = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(ExternalId)) yield return new("Payment id is required.", [nameof(ExternalId)]);
        if (Amount != decimal.Round(Amount, 2)) yield return new("Amount supports at most two decimal places.", [nameof(Amount)]);
        if (PaidAt > DateTimeOffset.UtcNow || PaidAt < DateTimeOffset.UnixEpoch)
            yield return new("PaidAt must be between 1970 and now.", [nameof(PaidAt)]);
        if (CurrentInstallment > TotalInstallments) yield return new("Installment exceeds total installments.", [nameof(CurrentInstallment)]);
    }
}

public sealed record SalesOrderResponse(string ClientId, string OrderId,
    bool ClientCreated, bool OrderCreated, int PaymentsCreated, int SubscriptionsCreated, bool Replayed = false);

public sealed record SalesImportError(string Code, string Message);
public sealed record SalesImportResult(SalesOrderResponse? Value, SalesImportError? Error)
{
    public static SalesImportResult Conflict(string code, string message) => new(null, new(code, message));
}
