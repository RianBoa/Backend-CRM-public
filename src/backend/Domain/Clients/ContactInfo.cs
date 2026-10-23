namespace backend.Domain.Clients;
public sealed record ContactInfo(

    string? PhoneNumber,
    string? Telegram,
    string? Discord,
    string? Instagram
)
{
    public static readonly ContactInfo Empty = new(null, null, null, null);

    // Factory method
    public static ContactInfo Create(
        string? phoneNumber = null,
        string? telegram = null,
        string? discord = null,
        string? instagram = null)
    {
        phoneNumber = Normalize(phoneNumber);
        telegram = Normalize(telegram);
        discord = Normalize(discord);
        instagram = Normalize(instagram);

        if (!string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.Length < 6)
            throw new ArgumentException("Phone number too short", nameof(phoneNumber));

        if (telegram is not null && telegram.Any(char.IsWhiteSpace))
            throw new ArgumentException("Telegram username cannot contain spaces", nameof(telegram));

        return new ContactInfo(phoneNumber, telegram, discord, instagram);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public ContactInfo Update(
        string? phoneNumber = null,
        string? telegram = null,
        string? discord = null,
        string? instagram = null)
        => this with
        {
            PhoneNumber = phoneNumber ?? PhoneNumber,
            Telegram = telegram ?? Telegram,
            Discord = discord ?? Discord,
            Instagram = instagram ?? Instagram
        };

    public override string ToString()
        => $"{PhoneNumber}, {Telegram}, {Discord}, {Instagram}";
}
