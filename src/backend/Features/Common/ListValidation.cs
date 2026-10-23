using NUlid;

namespace backend.Features.Common;

public sealed class ListValidation
{
    public Dictionary<string, string[]> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ListValidation Paging(int? page, int? pageSize)
    {
        if (page is < 1 or > 1_000_000) Errors["page"] = ["page must be between 1 and 1000000."];
        if (pageSize is < 1 or > 100) Errors["pageSize"] = ["pageSize must be between 1 and 100."];
        return this;
    }

    public ListValidation Text(string? value, string field, int maxLength = 120)
    {
        if (value?.Length > maxLength) Errors[field] = [$"{field} must not exceed {maxLength} characters."];
        return this;
    }

    public ListValidation Sorting(string? field, string? direction, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(field) && !allowed.Contains(field.Trim(), StringComparer.OrdinalIgnoreCase))
            Errors["sortBy"] = [$"Allowed fields: {string.Join(", ", allowed)}."];
        if (!string.IsNullOrWhiteSpace(direction) &&
            !new[] { "asc", "desc" }.Contains(direction.Trim(), StringComparer.OrdinalIgnoreCase))
            Errors["sortDir"] = ["sortDir must be asc or desc."];
        return this;
    }

    public ListValidation Identifier(string? value, string field)
    {
        if (!string.IsNullOrWhiteSpace(value) && !Ulid.TryParse(value.Trim(), out _))
            Errors[field] = [$"{field} must be a valid ULID."];
        return this;
    }

    public ListValidation Positive(int? value, string field)
    {
        if (value is <= 0) Errors[field] = [$"{field} must be positive."];
        return this;
    }

    public ListValidation EnumValue<T>(string? value, string field) where T : struct, Enum
        => OneOf(value, field, Enum.GetNames<T>());

    public ListValidation OneOf(string? value, string field, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            Errors[field] = [$"Allowed values: {string.Join(", ", allowed)}."];
        return this;
    }

    public ListValidation Dates(DateOnly? from, DateOnly? to)
    {
        if (from > to) Errors["createdTo"] = ["createdTo must not precede createdFrom."];
        return this;
    }

    public ListValidation Dates(DateTime? from, DateTime? to)
    {
        Utc(from, "createdFrom");
        Utc(to, "createdTo");
        if (from > to) Errors["createdTo"] = ["createdTo must not precede createdFrom."];
        return this;
    }

    public ListValidation Utc(DateTime? value, string field)
    {
        if (value is { Kind: not DateTimeKind.Utc }) Errors[field] = [$"{field} must include UTC timezone Z."];
        return this;
    }
}
