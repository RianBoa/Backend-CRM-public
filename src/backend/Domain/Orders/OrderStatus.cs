namespace backend.Domain.Orders
{
    public sealed record OrderStatus
    {
        public string Value { get; }

        private OrderStatus() { }
        private OrderStatus(string value)
        {
            var allowedStatuses = new HashSet<string>
            {
                "New", "PreLearning", "Active", "Graduated", "Frozen", "Closed"
            };

            if (!allowedStatuses.Contains(value))
                throw new ArgumentException($"Invalid order status: {value}.", nameof(value));

            Value = value;
        }

        public override string ToString() => Value;

        public static OrderStatus FromValue(string value) =>
            value switch
            {
                "New" => New,
                "PreLearning" => PreLearning,
                "Active" => Active,
                "Graduated" => Graduated,
                "Frozen" => Frozen,
                "Closed" => Closed,
                _ => new OrderStatus(value)
            };

        public static OrderStatus New => new("New");
        public static OrderStatus PreLearning => new("PreLearning");
        public static OrderStatus Active => new("Active");
        public static OrderStatus Graduated => new("Graduated");
        public static OrderStatus Frozen => new("Frozen");
        public static OrderStatus Closed => new("Closed");
    }
}
