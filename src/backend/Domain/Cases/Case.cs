namespace backend.Domain.Cases
{
    using NUlid;
    using backend.Domain.Clients;
    using backend.Domain.Users;
    public sealed record Case
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid ClientId { get; private init; }
        public Client Client { get; private init; } = null!;
        public enum CaseType
        {
            Bot,
            Futures,
            Ipso,
            Spot,
            NoBudget,
            Other
        }
        public CaseType Type { get; private init; }
        public decimal Amount { get; private init; }
        public string Currency { get; private init; } = "USD";
        public string? ScreenshotUrl { get; private init; }
        public string? TelegramUrl { get; private init; }
        public string? Comment { get; private init; }
        public bool IsFeatured { get; private set; } = false;
        public bool HasConsent { get; private set; } = true;
        public enum CaseStatus
        {
            New,
            Reviewed,
            Approved,
            Rejected,
            Published
        }
        public CaseStatus Status { get; private set; } = CaseStatus.New;
        public DateTime CreatedAt { get; private init; } = DateTime.UtcNow;
        public Ulid CreatedById { get; private init; }
        public User CreatedBy { get; private init; } = null!;

        private Case() { }

        public static Case Create(
            Ulid clientId,
            Ulid createdById,
            CaseType type,
            decimal amount,
            string currency = "USD",
            string? screenshotUrl = null,
            string? telegramUrl = null,
            string? comment = null,
            bool isFeatured = false,
            bool hasConsent = true)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

            if (string.IsNullOrWhiteSpace(currency))
                throw new ArgumentException("Currency is required.", nameof(currency));

            return new Case
            {
                ClientId = clientId,
                CreatedById = createdById,
                Type = type,
                Amount = decimal.Round(amount, 2),
                Currency = currency.Trim().ToUpperInvariant(),
                ScreenshotUrl = NormalizeOptional(screenshotUrl),
                TelegramUrl = NormalizeOptional(telegramUrl),
                Comment = NormalizeOptional(comment),
                IsFeatured = isFeatured,
                HasConsent = hasConsent
            };
        }

        public void ChangeStatus(CaseStatus status)
        {
            Status = status;
        }

        public void SetFeatured(bool isFeatured)
        {
            IsFeatured = isFeatured;
        }

        public void SetConsent(bool hasConsent)
        {
            HasConsent = hasConsent;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
