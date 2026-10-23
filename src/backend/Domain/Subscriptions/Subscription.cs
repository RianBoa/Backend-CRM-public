namespace backend.Domain.Subscriptions
{
    using backend.Domain.Clients;
    using backend.Domain.Orders;
    using backend.Domain.Products;
    using NUlid;

    public sealed record Subscription
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();

        public Ulid ClientId { get; private init; }
        public Client Client { get; private init; }

        public int ProductId { get; private init; }
        public Product Product { get; private init; }

        public Ulid OrderItemId { get; private init; }
        public OrderItem OrderItem { get; private init; }

        public DateTime? StartAt { get; private set; }
        public DateTime? EndAt { get; private set; }
        public int? DurationDays { get; private init; }
        public string? ProvisioningKey { get; private init; }

        public enum SubscriptionType
        {
            DiscordStandalone,
            DiscordCourseBonus,
            CoursePlatform,
            PackageAddon
        }
        public enum SubscriptionStatus
        {
            Active,
            Canceled,
            Suspended,
            Expired,
            AwaitingAccess
        }

        public SubscriptionType Type { get; private init; }
        public SubscriptionStatus Status { get; private set; }

        public string? Notes { get; private init; }

        public ICollection<DiscordAccess> DiscordAccesses { get; private set; } = new List<DiscordAccess>();
        public ICollection<PlatformAccess> PlatformAccesses { get; private set; } = new List<PlatformAccess>();
        public ICollection<SubscriptionAdjustment> Adjustments { get; private set; } = new List<SubscriptionAdjustment>();

        private Subscription() { }

        public static Subscription Create(
            Ulid clientId,
            Product product,
            OrderItem orderItem,
            DateTime startAt,
            DateTime endAt,
            SubscriptionType type,
            SubscriptionStatus status,
            string? notes = null)
        {
            if (status == SubscriptionStatus.AwaitingAccess)
                throw new ArgumentException("Use AwaitAccess to create a subscription without access dates.", nameof(status));
            if (orderItem.ProductId != product.Id || orderItem.Order.ClientId != clientId)
                throw new ArgumentException("Subscription must match the purchased product and client.");
            if (endAt <= startAt)
                throw new ArgumentException("End date must be after start date", nameof(endAt));

            return new Subscription
            {
                ClientId = clientId,

                ProductId = product.Id,
                Product = product,
                OrderItemId = orderItem.Id,
                OrderItem = orderItem,
                StartAt = startAt,
                EndAt = endAt,
                Type = type,
                Status = status,
                Notes = notes
            };
        }

        public static Subscription AwaitAccess(OrderItem purchase, SubscriptionType type, int durationDays)
        {
            ArgumentNullException.ThrowIfNull(purchase);
            if (durationDays <= 0) throw new ArgumentOutOfRangeException(nameof(durationDays));
            return new Subscription
            {
                ClientId = purchase.Order.ClientId,
                ProductId = purchase.ProductId,
                Product = purchase.Product,
                OrderItemId = purchase.Id,
                OrderItem = purchase,
                Type = type,
                Status = SubscriptionStatus.AwaitingAccess,
                DurationDays = durationDays,
                ProvisioningKey = $"{purchase.Id}:{type}"
            };
        }

        public bool ActivateAccess(DateTime grantedAtUtc)
        {
            if (grantedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Access grant timestamp must be UTC.", nameof(grantedAtUtc));
            if (Status == SubscriptionStatus.Active && StartAt is not null) return false;
            if (Status != SubscriptionStatus.AwaitingAccess || DurationDays is null)
                throw new InvalidOperationException("Only a subscription awaiting access can be activated.");

            var endAt = grantedAtUtc.AddDays(DurationDays.Value);
            StartAt = grantedAtUtc;
            EndAt = endAt;
            Status = SubscriptionStatus.Active;
            return true;
        }

        public bool ResumeAccess(DateTime nowUtc)
        {
            if (nowUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Resume timestamp must be UTC.", nameof(nowUtc));
            if (Status is not (SubscriptionStatus.Suspended or SubscriptionStatus.Active))
                throw new InvalidOperationException("Only a suspended subscription can be resumed.");
            if (StartAt is null || EndAt is null || StartAt > nowUtc || EndAt <= nowUtc)
                throw new InvalidOperationException("Access must have started and must not have expired before resuming.");
            if (Status == SubscriptionStatus.Active) return false;

            // Suspension does not pause the purchased term or extend its expiry.
            Status = SubscriptionStatus.Active;
            return true;
        }

        public bool Cancel()
        {
            if (Status == SubscriptionStatus.Canceled) return false;
            if (Status == SubscriptionStatus.Expired)
                throw new InvalidOperationException("Expired access is terminal.");
            Status = SubscriptionStatus.Canceled;
            return true;
        }

        public bool CancelPending()
        {
            if (StartAt is not null || Status is not (SubscriptionStatus.AwaitingAccess or SubscriptionStatus.Canceled))
                throw new InvalidOperationException("Only access that has not started can be canceled by this command.");
            return Cancel();
        }

        public bool Suspend()
        {
            if (Status == SubscriptionStatus.Suspended) return false;
            if (Status != SubscriptionStatus.Active)
                throw new InvalidOperationException("Only active access can be suspended.");
            Status = SubscriptionStatus.Suspended;
            return true;
        }

        public bool MarkAsExpired()
        {
            if (Status == SubscriptionStatus.Expired) return false;
            if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Suspended))
                throw new InvalidOperationException("Only started access can expire.");
            Status = SubscriptionStatus.Expired;
            return true;
        }

        public bool ChangeStatus(SubscriptionStatus next, DateTime nowUtc)
        {
            if (!Enum.IsDefined(next)) throw new ArgumentException("Unknown subscription status.", nameof(next));
            if (Status == next) return false;
            if (Status == SubscriptionStatus.AwaitingAccess)
                throw new InvalidOperationException("Use activate-access or cancel-pending for access that has not started.");
            return next switch
            {
                SubscriptionStatus.Canceled => Cancel(),
                SubscriptionStatus.Suspended => Suspend(),
                SubscriptionStatus.Expired => MarkAsExpired(),
                SubscriptionStatus.Active => ResumeAccess(nowUtc),
                _ => throw new InvalidOperationException("Access cannot return to AwaitingAccess.")
            };
        }
    }
}
