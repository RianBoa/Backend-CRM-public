using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace backend.Features.Integrations.Sales;

internal static class SalesFingerprint
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Event(SalesOrderRequest request) => Hash(new
    {
        PersonId = request.Client.PipedrivePersonId, Email = request.Client.Email.Trim().ToLowerInvariant(),
        Name = request.Client.Name.Trim(), DealId = request.Order.PipedriveDealId,
        Order = Order(request.Order),
        Payments = request.Payments.OrderBy(x => x.ExternalId.Trim(), StringComparer.Ordinal).Select(Payment).ToArray()
    });

    public static string Order(SalesOrder order) => Hash(new
    {
        Currency = order.Currency.ToUpperInvariant(),
        PurchasedAt = order.PurchasedAt!.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        Items = order.Items.OrderBy(x => x.ProductCode.Trim(), StringComparer.Ordinal)
            .ThenBy(x => x.PackageCode.Trim(), StringComparer.Ordinal)
            .Select(x => new { Product = x.ProductCode.Trim(), Package = x.PackageCode.Trim(), Price = Money(x.Price), x.Quantity }).ToArray()
    });

    public static string Payment(SalesPayment payment) => Hash(new
    {
        Id = payment.ExternalId.Trim(), Amount = Money(payment.Amount), Currency = payment.Currency.ToUpperInvariant(),
        payment.Status, PaidAt = payment.PaidAt!.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        Provider = Optional(payment.Provider), Invoice = Optional(payment.Invoice),
        payment.TotalInstallments, payment.CurrentInstallment
    });

    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static string Money(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
}
