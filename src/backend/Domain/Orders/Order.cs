namespace backend.Domain.Orders
{
    using backend.Domain.Clients;
    using NUlid;

    public enum OrderSource {
     Telegram,
     Discord,
     Manual,
     PipeDrive,
     LandingPage,
     Other
    }

    public sealed record Order
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid ClientId { get; private init; }
        public Client Client { get; private init; }
        public OrderStatus Status { get; private set; }
        public OrderSource Source { get; private init; }
        public ICollection<OrderItem> Items { get; private init; } = new List<OrderItem>();
        public ICollection<Payment> Payments { get; private init; } = new List<Payment>();
        public ICollection<AppliedBenefit> AppliedBenefits { get; private init; } = new List<AppliedBenefit>();
        public decimal TotalAmount => Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);

        public DateOnly CreatedAt { get; private init; }

        private Order() { }
        private Order(Ulid clientId, OrderStatus status, OrderSource source, DateOnly createdAt)
        {
            ClientId = clientId;
            Status = status;
            Source = source;
            CreatedAt = createdAt;
        }

        // Factory method to create a new Order
        public static Order Create(Ulid clientId, OrderSource source, DateOnly? createdAt = null)
        {
            return new Order(
                clientId: clientId,
                status: OrderStatus.New,
                source: source,
                createdAt: createdAt ?? DateOnly.FromDateTime(DateTime.UtcNow)
            );
        }
        public static Order Update(
            Order order,
            OrderStatus? status = null,
            OrderSource? source = null)
        {
            return order with
            {
                Status = status ?? order.Status,
                Source = source ?? order.Source
            };
        }
        public void MarkAsGraduated()
        {
            if (Status != OrderStatus.Active)
                throw new InvalidOperationException("Только активный заказ можно перевести в 'Graduated'");

            Status = OrderStatus.Graduated;
        }
    }
}
