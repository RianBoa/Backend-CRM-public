using System.ComponentModel.DataAnnotations;

namespace backend.Features.Clients.Profile;

// PUT replaces this block; null clears an optional field.
public sealed record ClientProfile : IValidatableObject
{
    [Required, StringLength(120)] public string Name { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    [StringLength(10)] public string? PreferredLanguage { get; init; }
    [StringLength(100)] public string? Source { get; init; }
    [StringLength(200)] public string? City { get; init; }
    public DateOnly? BirthDate { get; init; }
    [StringLength(200)] public string? Occupation { get; init; }

    public ClientProfile(string name, string? notes, string? preferredLanguage, string? source,
        string? city, DateOnly? birthDate, string? occupation)
        => (Name, Notes, PreferredLanguage, Source, City, BirthDate, Occupation)
            = (name, notes, preferredLanguage, source, city, birthDate, occupation);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BirthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new ValidationResult("Birth date cannot be in the future.", [nameof(BirthDate)]);
    }
}
