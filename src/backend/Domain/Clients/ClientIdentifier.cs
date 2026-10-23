namespace backend.Domain.Clients
{
    using NUlid;

    public sealed record ClientIdentifier
    {
        public Ulid Id { get; init; } = Ulid.NewUlid();
        public Ulid ClientId { get; init; }
        public string Source { get; init; } = null!; // e.g., "Telegram", "Discord", "Manual", etc.
        public string Value { get; init; } = null!; // e.g., username, email, etc.

        private ClientIdentifier() { }
        public static ClientIdentifier Create(Ulid clientId, string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source cannot be empty", nameof(source));
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty", nameof(value));
            return new ClientIdentifier
            {
                ClientId = clientId,
                Source = source,
                Value = value
            };
        }
        public static ClientIdentifier Update(ClientIdentifier identifier, string? source = null, string? value = null)
        {
            return identifier with
            {
                Source = source ?? identifier.Source,
                Value = value ?? identifier.Value
            };
        }
        public override string ToString() => $"{Source}: {Value}";

    }
}
