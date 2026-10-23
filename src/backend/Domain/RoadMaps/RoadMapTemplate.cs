namespace backend.Domain.RoadMaps
{
    using NUlid;
    using System.Runtime.InteropServices;

    public sealed record RoadMapTemplate
    {
        public int Id { get; private init; }
        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public int ProductId { get; private init; }
        public Products.Product Product { get; private init; }
        public bool IsActive { get; private set; } = true;

        private RoadMapTemplate() { }

        public static RoadMapTemplate Create(
            string name,
            int productId,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be empty.", nameof(name));
            return new RoadMapTemplate
            {
                Name = name,
                ProductId = productId,
                Description = description
            };
        }

        public void Update(
            string name,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be empty.", nameof(name));
            Name = name;
            Description = description;
        }
        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;
    }
}
