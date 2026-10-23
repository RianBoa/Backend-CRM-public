namespace backend.Domain.Products
{
    public sealed record Package
    {
        public int Id { get; init; }
        public string Code { get; init; }
        public string Name { get; init; }
        public int? DurationDays { get; init; }
        public bool DiscordAccess { get; init; }
        public int? DiscordDays { get; init; }
        public Product Product { get; init; }
        public int ProductId { get; init; }

        // Factory method to create a new Package instance

        public static Package Create(
            string code,
            string name,
            Product product,
            int? durationDays = null,
            bool discordAccess = false,
            int? discordDays = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Package code can not be empty.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Package name can not be empty.", nameof(name));
            if (product == null)
                throw new ArgumentNullException(nameof(product), "Product cannot be null.");
            if (durationDays.HasValue && durationDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationDays), "Duration days must be positive if specified.");
            if (discordDays.HasValue && discordDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(discordDays), "Discord days must be positive if specified.");
            return new Package
            {
                Code = code,
                Name = name,
                Product = product,
                ProductId = product.Id,
                DurationDays = durationDays,
                DiscordAccess = discordAccess,
                DiscordDays = discordDays
            };
        }

        public Package Update(
            string? name = null,
            int? durationDays = null,
            bool? discordAccess = null,
            int? discordDays = null)
        {
            if (durationDays.HasValue && durationDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationDays), "Duration days must be positive if specified.");
            if (discordDays.HasValue && discordDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(discordDays), "Discord days must be positive if specified.");
            return this with
            {
                Name = name ?? this.Name,
                DurationDays = durationDays ?? this.DurationDays,
                DiscordAccess = discordAccess ?? this.DiscordAccess,
                DiscordDays = discordDays ?? this.DiscordDays,
            };
        }
    }
}
