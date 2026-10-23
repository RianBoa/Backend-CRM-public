using NUlid;

namespace backend.Domain.Integrations;

public sealed class SalesOrderLink
{
    public const string SourceName = "sales";
    public string Source { get; private init; } = SourceName;
    public string DealId { get; private init; } = null!;
    public Ulid OrderId { get; private init; }
    public string Currency { get; private init; } = null!;
    public string ItemsFingerprint { get; private init; } = null!;
    public DateTimeOffset PurchasedAt { get; private init; }
    private SalesOrderLink() { }
    public static SalesOrderLink Create(string dealId, Ulid orderId, string currency, string fingerprint, DateTimeOffset purchasedAt)
        => new() { DealId = dealId, OrderId = orderId, Currency = currency, ItemsFingerprint = fingerprint, PurchasedAt = purchasedAt.ToUniversalTime() };
}

public sealed class SalesPaymentLink
{
    public string Source { get; private init; } = SalesOrderLink.SourceName;
    public string ExternalId { get; private init; } = null!;
    public Ulid PaymentId { get; private init; }
    public Ulid OrderId { get; private init; }
    public string Fingerprint { get; private init; } = null!;
    private SalesPaymentLink() { }
    public static SalesPaymentLink Create(string externalId, Ulid paymentId, Ulid orderId, string fingerprint)
        => new() { ExternalId = externalId, PaymentId = paymentId, OrderId = orderId, Fingerprint = fingerprint };
}

public sealed class SalesImportReceipt
{
    public string Source { get; private init; } = SalesOrderLink.SourceName;
    public string EventId { get; private init; } = null!;
    public string Fingerprint { get; private init; } = null!;
    public string ResponseJson { get; private init; } = null!;
    public DateTimeOffset ReceivedAt { get; private init; }
    private SalesImportReceipt() { }
    public static SalesImportReceipt Create(string eventId, string fingerprint, string responseJson, DateTimeOffset receivedAt)
        => new() { EventId = eventId, Fingerprint = fingerprint, ResponseJson = responseJson, ReceivedAt = receivedAt };
}
