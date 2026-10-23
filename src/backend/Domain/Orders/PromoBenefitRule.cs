namespace backend.Domain.Orders
{
    using Domain.Products;
    using System.Security.Cryptography;

    public class PromoBenefitRule
    {
        public int Id { get; private init; }
        public string RuleCode { get; private init; }
        public bool IsActive { get; private set; }

        public int? ProductId { get; private init; }
        public Product? Product { get; private init; }
        public int? PackageId { get; private init; }
        public Package? Package { get; private init; }
        public enum BenefitType
        {
            DiscountPercentage,
            DiscountFixedAmount,
            FreeItem,
            AdditionalDays,
            Other
        }
        public  BenefitType Type { get; private init; }
        public decimal? DiscountPercentage { get; private init; }
        public decimal? DiscountFixedAmount { get; private init; }
        public int? EffectDays { get; private init; }
        public Product? BonusProduct { get; private init; }
        public int? BonusProductId { get; private init; }
        public Package? BonusPackage { get; private init; }
        public int BonusPackageId { get; private init; }
        public string Name { get; private init; }
        public string Description { get; private init; }
        public DateTime? StartAt { get; private set; }
        public DateTime? EndAt { get; private set; }

        public ICollection<AppliedBenefit> AppliedBenefits { get; private init; } = new List<AppliedBenefit>();

        private PromoBenefitRule() { }

        public bool IsCurrentlyValid(DateTime now)
        {
            return IsActive
                   && (StartAt == null || StartAt <= now)
                   && (EndAt == null || EndAt >= now);
        }

        public static PromoBenefitRule Create(
            string ruleCode,
            string name,
            string description,
            BenefitType type,
            DateTime startAt,
            bool isActive = true,
            int? productId = null,
            int? packageId = null,
            decimal? discountPercentage = null,
            decimal? discountFixedAmount = null,
            int? effectDays = null,
            int? bonusProductId = null,
            int? bonusPackageId = null,
            DateTime? endAt = null)
        {
            if (string.IsNullOrWhiteSpace(ruleCode))
                throw new ArgumentException("Rule code cannot be empty.", nameof(ruleCode));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be empty.", nameof(description));
            if (startAt == default)
                throw new ArgumentException("Start date must be a valid date.", nameof(startAt));
            switch (type)
            {
                case BenefitType.DiscountPercentage:
                    if (!discountPercentage.HasValue || discountPercentage <= 0 || discountPercentage > 100)
                        throw new ArgumentOutOfRangeException(nameof(discountPercentage), "Discount percentage must be between 0 and 100.");
                    break;
                case BenefitType.DiscountFixedAmount:
                    if (!discountFixedAmount.HasValue || discountFixedAmount <= 0)
                        throw new ArgumentOutOfRangeException(nameof(discountFixedAmount), "Discount fixed amount must be positive.");
                    break;
                case BenefitType.AdditionalDays:
                    if (!effectDays.HasValue || effectDays <= 0)
                        throw new ArgumentOutOfRangeException(nameof(effectDays), "Effect days must be positive.");
                    break;
                case BenefitType.FreeItem:
                    if (!bonusProductId.HasValue && !bonusPackageId.HasValue)
                        throw new ArgumentException("Either bonus product ID or bonus package ID must be provided for FreeItem benefit type.");
                    break;
                case BenefitType.Other:
                    // No specific validation for Other type
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Invalid benefit type.");
            }
            return new PromoBenefitRule
            {
                RuleCode = ruleCode,
                Name = name,
                Description = description,
                Type = type,
                IsActive = true,
                StartAt = startAt,
                EndAt = endAt,
                ProductId = productId,

            };

        }

    }
}
