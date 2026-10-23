using NUlid;

namespace backend.Domain.Clients
{
    public sealed record ExternalAccount
    {
        public Ulid Ulid { get; private init; } = Ulid.NewUlid();
        public Ulid ClientId { get; private init; }
        public string Provider { get; private init; } = null!;
        public string ExternalId { get; private init; } = null!;
        private ExternalAccount() { } // EF Core

        private ExternalAccount(Ulid clientId, string provider, string externalId)
        {
            ClientId = clientId;
            Provider = provider.Trim().ToLowerInvariant();
            ExternalId = externalId.Trim();
        }

        public static ExternalAccount Create(Ulid clientId, string provider, string externalId)
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentException("Provider cannot be empty.", nameof(provider));

            if (string.IsNullOrWhiteSpace(externalId))
                throw new ArgumentException("ExternalId cannot be empty.", nameof(externalId));

            return new ExternalAccount(clientId, provider, externalId);
        }
    }
}
