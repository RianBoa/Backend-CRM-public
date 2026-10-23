using System.Text.Json;
using backend.Domain.Orders;
using backend.Domain.Users;
using backend.Features.Common;
using backend.Features.Products.Management;
using NUlid;
using Xunit;

namespace Backend.Tests;

public sealed class ApiInputRulesTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1000001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPagingCannotBecomeAnUnboundedOrOverflowingQuery(int page, int pageSize)
        => Assert.NotEmpty(new ListValidation().Paging(page, pageSize).Errors);

    [Fact]
    public void OptionalDefaultsAndCanonicalSortNamesAreAccepted()
        => Assert.Empty(new ListValidation().Paging(null, null).Sorting(" NAME ", " DESC ", ["name"]).Errors);

    [Fact]
    public void UnknownSortFilterAndIdentifierAreRejected()
    {
        var errors = new ListValidation().Sorting("passwordHash", "random", ["name"])
            .EnumValue<User.UserRole>("SuperOwner", "role").Identifier("123", "clientId").Errors;
        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public void UnknownJsonFieldsCannotSilentlyDisappearFromAnEdit()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PatchProductRequest>(
            "{\"name\":\"Course\",\"adminOverride\":true}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public void OptionalPatchTextMayBeOmittedButNotBlank()
    {
        var rule = new NotBlankAttribute();
        Assert.True(rule.IsValid(null));
        Assert.True(rule.IsValid("Course"));
        Assert.False(rule.IsValid("  "));
    }

    [Fact]
    public void TimestampWithoutTimezoneIsNotAcceptedForPostgresTimestamptz()
        => Assert.False(new UtcDateTimeAttribute().IsValid(new DateTime(2026, 10, 23)));

    [Theory]
    [InlineData("USD", "paid", "USD", "Paid")]
    [InlineData(" usdt ", " pending ", "USDT", "Pending")]
    public void PaymentTermsAreNormalized(string currency, string status, string expectedCurrency, string expectedStatus)
    {
        var payment = Payment.Create(Ulid.NewUlid(), 10, currency, status);
        Assert.Equal(expectedCurrency, payment.Currency);
        Assert.Equal(expectedStatus, payment.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(0.001)]
    [InlineData(10000000000)]
    public void InvalidMoneyIsRejectedBeforePersistence(double amount)
        => Assert.Throws<ArgumentException>(() => Payment.Create(Ulid.NewUlid(), (decimal)amount, "USD"));

    [Fact]
    public void InstallmentCannotExceedItsPlan()
        => Assert.Throws<ArgumentException>(() => Payment.Create(Ulid.NewUlid(), 10, "USD", "Paid", 2, 3));
}
