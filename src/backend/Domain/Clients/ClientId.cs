using System.Text.RegularExpressions;

namespace backend.Domain.Clients
{
    public sealed record ClientId
    {
        public string Value { get; }

        private static readonly Regex HexRe = new("^[A-Fa-f0-9]{5,9}$", RegexOptions.Compiled);

        private ClientId(string value)
        {
            if (!HexRe.IsMatch(value))
                throw new ArgumentException("ClientId must be a hex string 5–9 chars.", nameof(value));
            Value = value.ToLowerInvariant();
        }

        public static ClientId From(string value) => new(value);
        public static bool TryFrom(string? value, out ClientId? id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!HexRe.IsMatch(value)) return false;
            id = new ClientId(value);
            return true;
        }

        public static ClientId GenerateNew()
        {

            var hex = Guid.NewGuid().ToString("N")[..7];
            return new ClientId(hex);
        }

        public override string ToString() => Value;
    }
}
