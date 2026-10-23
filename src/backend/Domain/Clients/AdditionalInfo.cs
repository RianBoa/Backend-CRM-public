namespace backend.Domain.Clients;


public sealed record AdditionalInfo
{
    public string? Notes { get; }
    public string? PreferredLanguage { get; set; }
    public string? Source { get; }
    public string? City { get; }
    public string? Occupation { get; }
    public DateOnly? BirthDate { get; }
    private AdditionalInfo() { }


    public static readonly AdditionalInfo Empty = new(null, null, null, null, null) ;

    public AdditionalInfo(string? comment, string? source, string? city, DateOnly? birthDate, string? occupation)
    {
        Notes = NormalizeOrNull(comment, 2000);
        Source = NormalizeOrNull(source, 100);
        City = NormalizeOrNull(city, 200);
        BirthDate = birthDate;
        Occupation = occupation;
    }

    private static string? NormalizeOrNull(string? v, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var norm = v.Trim();
        return norm.Length > maxLen ? norm[..maxLen] : norm;
    }

    public AdditionalInfo WithComment(string? comment)
        => new(comment, Source, City, BirthDate, Occupation);

    public AdditionalInfo WithSource(string? source)
        => new(Notes, source, City, BirthDate, Occupation);

    public AdditionalInfo WithCity(string? city)
        => new(Notes, Source, city, BirthDate, Occupation);

    public AdditionalInfo WithBirthDate(DateOnly? birthDate)
        => new(Notes, Source, City, birthDate, Occupation);

    public AdditionalInfo WithAgeRange(string? occupation)
        => new(Notes, Source, City, BirthDate, occupation);

    public override string ToString()
        => $"Comment: {Notes ?? "-"}, Source: {Source ?? "-"}, City: {City ?? "-"}, BirthDate: {BirthDate?.ToString("yyyy-MM-dd") ?? "-"}, Occupation: {Occupation ?? "-"}";
}
