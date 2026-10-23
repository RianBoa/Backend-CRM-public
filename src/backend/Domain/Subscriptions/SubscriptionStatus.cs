namespace backend.Domain.Subscriptions
{

    public sealed record SubscriptionStatus(string Value, bool IsTerminal = false, string? Reason = null)

    {

        public static readonly SubscriptionStatus New = new("New");
        public static readonly SubscriptionStatus Active = new("Active");
        public static readonly SubscriptionStatus Suspended = new("Suspended");
        public static readonly SubscriptionStatus Canceled = new("Canceled", IsTerminal: true);
        public static readonly SubscriptionStatus Expired = new("Expired", IsTerminal: true);
        public static readonly SubscriptionStatus Graduated = new("Graduated", IsTerminal: true);

        public static SubscriptionStatus SuspendedWithReason(string reason) =>
            new("Suspended", IsTerminal: false, reason);

        public bool CanTransitionTo(SubscriptionStatus next)
        {
            if (IsTerminal) return false;

            return (Value, next.Value) switch
            {
                ("New", "Active") => true,
                ("Active", "Suspended") => true,
                ("Active", "Canceled") => true,
                ("Active", "Expired") => true,
                ("Active", "Graduated") => true,
                ("Suspended", "Active") => true,
                ("Suspended", "Canceled") => true,
                _ => false
            };
        }

        public override string ToString() => Value;
    }
}
