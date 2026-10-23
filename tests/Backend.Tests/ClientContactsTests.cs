using System.ComponentModel.DataAnnotations;
using backend.Domain.Clients;
using backend.Features.Clients.Contacts;
using Xunit;

namespace Backend.Tests;

public sealed class ClientContactsTests
{
    [Fact]
    public void RemovingLastContactLeavesExistingContactIntact()
    {
        var client = Client.Create("Student", null, ContactInfo.Create(telegram: "@student"));

        Assert.Throws<ContactMethodRequiredException>(() => client.SetContacts(ContactInfo.Empty));

        Assert.Equal("@student", client.Contacts.Telegram);
    }

    [Fact]
    public void EmailAllowsClearingAllOtherContacts()
    {
        var client = Client.Create("Student", Email.From("student@example.test"));

        client.SetContacts(ContactInfo.Empty);

        Assert.Equal(ContactInfo.Empty, client.Contacts);
        Assert.Single(client.Emails);
        Assert.Equal("student@example.test", client.PrimaryEmail!.Value);
    }

    [Fact]
    public void InstagramAloneDoesNotReplaceRequiredContactMethod()
    {
        var client = Client.Create("Student", null, ContactInfo.Create(telegram: "@student"));

        Assert.Throws<ContactMethodRequiredException>(() =>
            client.SetContacts(ContactInfo.Create(instagram: "@student")));

        Assert.Equal("@student", client.Contacts.Telegram);
    }

    [Fact]
    public void ReplacingContactsDoesNotChangeProfileOrEmails()
    {
        var client = Client.Create("Student", Email.From("student@example.test"));
        client.SetAdditionalInfo(new AdditionalInfo("Profile note", null, "Kyiv", null, null));

        client.SetContacts(ContactInfo.Create(telegram: "@new_handle"));

        Assert.Equal("Student", client.Name);
        Assert.Equal("Profile note", client.AdditionalInfo!.Notes);
        Assert.Equal("student@example.test", client.PrimaryEmail!.Value);
    }

    [Fact]
    public void FormNormalizesWhitespaceAndClearsBlankFields()
    {
        var form = new ClientContactsBlock(" +380671234567 ", " @student ", " ", null);

        var contacts = form.ToContactInfo();

        Assert.Equal("+380671234567", contacts.PhoneNumber);
        Assert.Equal("@student", contacts.Telegram);
        Assert.Null(contacts.Discord);
    }

    [Theory]
    [InlineData("123", null)]
    [InlineData(null, "two handles")]
    [InlineData(null, "two\thandles")]
    public void InvalidContactFieldsAreRejected(string? phone, string? telegram)
    {
        var form = new ClientContactsBlock(phone, telegram, null, null);
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), errors, true));
        Assert.NotEmpty(errors);
    }
}
