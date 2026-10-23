namespace backend.Domain.Subscriptions
{
    using NUlid;
    using backend.Domain.Orders;
    public sealed record SubscriptionAdjustment
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid SubscriptionId { get; private init; }
        public Subscription Subscription { get; private init; }

        public enum AdjustmentType
        {
            Extension,       // +N
            Reduction,       // -N
            ManualOverride,
            Upgrade,
            Pause,
            Resume
        }

        public AdjustmentType Type { get; private init; }

        public DateTime AdjustmentDate { get; private init; }
        public int? DaysChanged { get; private init; }          // +N / -N
        public DateTime? OldEndDate { get; private init; }
        public DateTime? NewEndDate { get; private init; }

        public string? NewRole { get; private init; }
        public string? Reason { get; private init; }

        public string Source { get; private init; }             // system|order|manual|compensation
        public Ulid? RelatedOrderId { get; private init; }
        public string? CreatedBy { get; private init; }

        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;

        private SubscriptionAdjustment() { }

        public SubscriptionAdjustment Create(
            Ulid subscriptionId,
            AdjustmentType type,
            DateTime adjustmentDate,
            int? daysChanged = null,
            DateTime? oldEndDate = null,
            DateTime? newEndDate = null,
            string? newRole = null,
            string? reason = null,
            string source = "manual",
            Ulid? relatedOrderId = null,
            string? createdBy = null)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source is required.", nameof(source));
            return new SubscriptionAdjustment
            {
                SubscriptionId = subscriptionId,
                Type = type,
                AdjustmentDate = adjustmentDate,
                DaysChanged = daysChanged,
                OldEndDate = oldEndDate,
                NewEndDate = newEndDate,
                NewRole = newRole,
                Reason = reason,
                Source = source,
                RelatedOrderId = relatedOrderId,
                CreatedBy = createdBy
            };
        }
    }
}
