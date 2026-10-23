using System.ComponentModel.DataAnnotations;

namespace backend.Features.Common;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NotBlankAttribute : ValidationAttribute
{
    public NotBlankAttribute() : base("{0} cannot be blank.") { }
    public override bool IsValid(object? value) => value is null || value is string text && !string.IsNullOrWhiteSpace(text);
}

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class UtcDateTimeAttribute : ValidationAttribute
{
    public UtcDateTimeAttribute() : base("{0} must include UTC timezone Z.") { }
    public override bool IsValid(object? value) => value is null || value is DateTime { Kind: DateTimeKind.Utc };
}
