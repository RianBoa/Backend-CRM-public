namespace backend.Domain.Subscriptions
{
    using NUlid;
    public sealed record DiscordAccess
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid SubscriptionId { get; private init; }
        public Subscription Subscription { get; private init; }
        public string DiscordUserId { get; private set; }
        public string RoleId { get; private set; }
        public string RoleName { get; private set; }
        public DateTime? GrantedAt { get; private init; }

        private DiscordAccess() { }

        public static DiscordAccess Create(
            Ulid subscriptionId,
            string discordUserId,
            string roleId,
            string roleName,
            DateTime? grantedAt = null)
        {
            if (string.IsNullOrWhiteSpace(discordUserId))
                throw new ArgumentException("Discord user ID is required.", nameof(discordUserId));
            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentException("Role ID is required.", nameof(roleId));
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.", nameof(roleName));
            return new DiscordAccess
            {
                SubscriptionId = subscriptionId,
                DiscordUserId = discordUserId,
                RoleId = roleId,
                RoleName = roleName,
                GrantedAt = grantedAt
            };
        }

    }
}
