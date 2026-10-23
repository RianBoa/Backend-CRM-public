namespace backend.Domain.Subscriptions
{
    using NUlid;
    public sealed record PlatformAccess
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid SubscriptionId { get; private init; }
        public Subscription Subscription { get; private init; }
        public string PlatformName { get; private set; }
        public string PlatformUserId { get; private set; }
        public DateTime? GrantedAt { get; private init; }
        public string? Notes { get; private init; }

        private PlatformAccess() { }

        public static PlatformAccess Create(
            Ulid subscriptionId,
            string platformName,
            string platformUserId,
            DateTime? grantedAt = null,
            string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(platformName))
                throw new ArgumentException("Platform name is required.", nameof(platformName));
            if (string.IsNullOrWhiteSpace(platformUserId))
                throw new ArgumentException("Platform user ID is required.", nameof(platformUserId));
            return new PlatformAccess
            {
                SubscriptionId = subscriptionId,
                PlatformName = platformName,
                PlatformUserId = platformUserId,
                GrantedAt = grantedAt,
                Notes = notes
            };
        }
    }
}
