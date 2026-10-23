namespace backend.Domain.Products
{
    public sealed record Product
    {
        public int Id { get; init; }
        public required string Code { get; init; }
        public string? LegacyCode { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
        public bool IsActive { get; init; }
        public bool IsArchived { get; init; }
        public bool DiscordAccess { get; init; }
        public DateOnly CreatedAt { get; init; }

        // Factory method to create a new Product instance
        public static Product Create(
        string code,
        string name,
        string type,
        bool discordAccess,
        string? legacyCode = null,
        DateOnly? createdAt = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Product code can not be empty.", nameof(code));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Product name can not be empty.", nameof(name));

            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type product can not be empty.", nameof(type));

            return new Product
            {
                Code = code,
                LegacyCode = legacyCode,
                Name = name,
                Type = type,
                IsActive = true,
                IsArchived = false,
                DiscordAccess = discordAccess,
                CreatedAt = createdAt ?? DateOnly.FromDateTime(DateTime.Today)
            };
        }


        public Product Update(
            string? code = null,
            string? name = null,
            string? type = null,
            bool? isActive = null,
            bool? isArchived = null,
            bool? discordAccess = null,
            string? legacyCode = null)
        {
            return this with
            {
                Code = code ?? this.Code,
                Name = name ?? this.Name,
                Type = type ?? this.Type,
                IsActive = isActive ?? this.IsActive,
                IsArchived = isArchived ?? this.IsArchived,
                DiscordAccess = discordAccess ?? this.DiscordAccess,
                LegacyCode = legacyCode ?? this.LegacyCode
            };
        }
    }
}
