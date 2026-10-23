using System.Text.RegularExpressions;

namespace backend.Domain.Clients;

public sealed record ClientKey
{
    public enum KeyType { NewId, LegacyCode }

    public KeyType Type { get; }
    public string Value { get; }
    public ClientId? AsClientId { get; }

    private ClientKey(KeyType type, string value, ClientId? id = null)
    {
        Type = type;
        Value = value;
        AsClientId = id;
    }

    private static readonly Regex LegacyOldFfaRe =
        new(@"^OldFFA\d{6,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(string? raw, out ClientKey? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();

        if (ClientId.TryFrom(raw, out var id))
        {
            key = new ClientKey(KeyType.NewId, id!.Value, id);
            return true;
        }

        if (LegacyOldFfaRe.IsMatch(raw))
        {
            key = new ClientKey(KeyType.LegacyCode, raw.ToUpperInvariant());
            return true;
        }

        return false;
    }
}
