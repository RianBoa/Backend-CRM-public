using System.ComponentModel.DataAnnotations;
using backend.Domain.Clients;

namespace backend.Features.Clients.Contacts;

public sealed record ClientContactsBlock : IValidatableObject
{
    [StringLength(32)] public string? PhoneNumber { get; init; }
    [StringLength(64)] public string? Telegram { get; init; }
    [StringLength(64)] public string? Discord { get; init; }
    [StringLength(128)] public string? Instagram { get; init; }

    public ClientContactsBlock(string? phoneNumber, string? telegram, string? discord, string? instagram)
        => (PhoneNumber, Telegram, Discord, Instagram) = (phoneNumber, telegram, discord, instagram);

    public ContactInfo ToContactInfo() => ContactInfo.Create(PhoneNumber, Telegram, Discord, Instagram);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ValidationResult? error = null;
        try { _ = ToContactInfo(); }
        catch (ArgumentException ex)
        {
            error = new ValidationResult(ex.Message, [ex.ParamName ?? "contacts"]);
        }
        if (error is not null) yield return error;
    }

    public static ClientContactsBlock From(ContactInfo contacts)
        => new(contacts.PhoneNumber, contacts.Telegram, contacts.Discord, contacts.Instagram);
}
