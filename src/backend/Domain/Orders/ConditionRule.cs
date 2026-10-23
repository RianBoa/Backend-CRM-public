namespace backend.Domain.Orders
{
    using backend.Domain.Products;
    using NUlid;
    using System;

    public sealed record ConditionRule
    {
        public int Id { get; private init; }

        public OrderItem OrderItem { get; private init; }
        public Ulid? OrderItemId { get; private init; }

        public Product? Product { get; private init; }
        public int ProductId { get; private init; }

        public string ConditionText { get; private init; } = null!;
        public string Source { get; private init; } = null!; // manual|system|legacy

        public DateTime UpdatedAt { get; private init; }

        private ConditionRule() { }

        public static ConditionRule Create(
            OrderItem orderItem,
            Product product,
            string conditionText,
            string source,
            DateTime? updatedAt = null)
        {
            if (string.IsNullOrWhiteSpace(conditionText))
                throw new ArgumentException("Condition text cannot be empty", nameof(conditionText));

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source cannot be empty", nameof(source));

            return new ConditionRule
            {
                OrderItem = orderItem,
                OrderItemId = orderItem.Id,
                Product = product,
                ProductId = product.Id,
                ConditionText = conditionText,
                Source = source,
                UpdatedAt = updatedAt ?? DateTime.UtcNow
            };
        }
    }
}
