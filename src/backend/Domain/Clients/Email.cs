using System.Text.RegularExpressions;

namespace backend.Domain.Clients;

public sealed record Email
{
    public string Value { get; }

    // базовый regex для простого формата
    private static readonly Regex EmailRe = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private Email(string value) => Value = value;

    public static Email From(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Email cannot be empty.", nameof(raw));

        var norm = raw.Trim().ToLowerInvariant();

        if (norm.Length > 320)
            throw new ArgumentException("Email too long (>320).", nameof(raw));

        if (!EmailRe.IsMatch(norm))
            throw new ArgumentException("Invalid email format.", nameof(raw));

        if (!IsAscii(norm))
            throw new ArgumentException("Email must contain only ASCII characters (no кириллица).", nameof(raw));

        return new Email(norm);
    }

    public static bool TryFrom(string? raw, out Email? email)
    {
        email = null;
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            email = From(raw);
            return true;
        }
        catch { return false; }
    }

    public static Email? FromOrNull(string? raw)
        => TryFrom(raw, out var e) ? e : null;

    public override string ToString() => Value;

    private static bool IsAscii(string value)
        => value.All(c => c <= 127);
}
