using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using backend.Features.Integrations.Sales;
using Xunit;

namespace Backend.Tests;

public sealed class SalesContractTests
{
    [Fact]
    public void FingerprintIgnoresEmailCaseDecimalScaleAndPaymentOrdering()
    {
        var original = Request();
        var normalizedEquivalent = original with
        {
            Client = original.Client with { Email = " BUYER@example.test " },
            Order = original.Order with { Items = [new("course", "standard", 100.00m, 1)] },
            Payments = original.Payments.Reverse().ToArray()
        };
        Assert.Equal(SalesFingerprint.Event(original), SalesFingerprint.Event(normalizedEquivalent));
    }

    [Fact]
    public void ChangedPaymentAmountChangesTheFingerprint()
    {
        var original = Request();
        var changed = original with { Payments = [original.Payments[0] with { Amount = 30 }] };
        Assert.NotEqual(SalesFingerprint.Event(original), SalesFingerprint.Event(changed));
    }

    [Fact]
    public void MixedCurrenciesAreRejectedByCrossFieldValidation()
    {
        var request = Request();
        request = request with { Payments = [request.Payments[0] with { Currency = "EUR" }] };
        Assert.Contains(request.Validate(new ValidationContext(request)), x => x.MemberNames.Contains(nameof(request.Payments)));
    }

    [Fact]
    public void FinancialProfileFieldsCannotBeSmuggledIntoTheContract()
    {
        var json = JsonSerializer.Serialize(Request(), SalesFingerprint.Json);
        json = json.Insert(1, "\"initialBudget\":123,");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SalesOrderRequest>(json, SalesFingerprint.Json));
    }

    private static SalesOrderRequest Request()
    {
        var date = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        return new("event-1", new("12345", "buyer@example.test", "Buyer"),
            new("67890", "USD", [new("course", "standard", 100m, 1)], date),
            [new("payment-1", 25, "USD", "Paid", date), new("payment-2", 25, "USD", "Paid", date)]);
    }
}
