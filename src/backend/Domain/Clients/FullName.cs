using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace backend.Domain.Clients;

public sealed record FullName
{
    public string Value { get; }
    public string First { get; }
    public string? Middle { get; }
    public string? Last { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    private const int MaxLen = 200;

    private FullName(string value, string first, string? middle, string? last)
    {
        Value = value;
        First = first;
        Middle = string.IsNullOrWhiteSpace(middle) ? null : middle;
        Last = string.IsNullOrWhiteSpace(last) ? null : last;
    }


    public static FullName From(string raw)
    {
        var norm = Normalize(raw);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("Full name is required.", nameof(raw));
        if (norm.Length > MaxLen)
            throw new ArgumentException($"Full name is too long (>{MaxLen}).", nameof(raw));

        var (first, middle, last) = Split(norm);
        return new FullName(norm, first, middle, last);
    }

    public static FullName FromParts(string? first, string? middle = null, string? last = null)
    {
        var pieces = new[] { first, middle, last }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Where(s => !string.IsNullOrEmpty(s));

        var joined = string.Join(' ', pieces);
        return From(joined);
    }

    public static bool TryFrom(string? raw, out FullName? name)
    {
        name = null;
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            name = From(raw);
            return true;
        }
        catch { return false; }
    }

    public override string ToString() => Value;

    //helpers

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;


        var sb = new StringBuilder(s.Trim());
        for (int i = 0; i < sb.Length; i++)
        {
            if (char.IsWhiteSpace(sb[i])) sb[i] = ' ';
        }

        var singleSpaced = Regex.Replace(sb.ToString(), " {2,}", " ");


        var culture = CultureInfo.CurrentCulture;
        var words = singleSpaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => TitleWord(w, culture));
        return string.Join(' ', words);
    }

    private static string TitleWord(string w, CultureInfo culture)
    {
        int upper = 0, lower = 0;
        foreach (var ch in w)
        {
            if (char.IsUpper(ch)) upper++;
            else if (char.IsLower(ch)) lower++;
        }
        if (upper >= 2 && lower == 0) return w;

        var parts = Regex.Split(w, "([-'])");
        for (int i = 0; i < parts.Length; i += 2)
        {
            if (parts[i].Length == 0) continue;
            var p = parts[i];
            var first = char.ToUpper(p[0], culture);
            var rest = p.Length > 1 ? p[1..].ToLower(culture) : string.Empty;
            parts[i] = first + rest;
        }
        return string.Concat(parts);
    }

    private static (string first, string? middle, string? last) Split(string norm)
    {
        var parts = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return (parts[0], null, null);
        if (parts.Length == 2)
            return (parts[0], null, parts[1]);

        var first = parts[0];
        var last = parts[^1];
        var middle = string.Join(' ', parts.Skip(1).Take(parts.Length - 2));
        return (first, middle, last);
    }
}
