using System.ComponentModel.DataAnnotations;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Validates that a list of strings holds no null, empty or white-space entry.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class NonBlankItemsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IEnumerable<string?> items && items.Any(string.IsNullOrWhiteSpace))
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must not contain an empty entry.",
                validationContext.MemberName is null ? null : [validationContext.MemberName]);
        }

        return ValidationResult.Success;
    }
}
