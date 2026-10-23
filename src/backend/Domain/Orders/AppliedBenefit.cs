namespace backend.Domain.Orders
{
    using Domain.Products;
    using NUlid;

    public sealed record AppliedBenefit
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();

        public Ulid OrderId { get; private init; }
        public Order Order { get; private init; }
        public OrderItem OrderItem { get; private init; }
        public Ulid OrderItemId { get; private init; }

        public int PromoBenefitRuleId { get; private init; }
        public PromoBenefitRule PromoBenefitRule { get; private init; }

        private AppliedBenefit() { }

    }
}
