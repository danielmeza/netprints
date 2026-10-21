using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Validates that a string is empty (the operating system's culture) or the name of a culture, such as <c>es-MX</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed partial class CultureNameAttribute : ValidationAttribute
{
    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex WellFormed();

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string { Length: > 0 } name && !IsCultureName(name))
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must be empty or a culture name such as 'es-MX'.",
                validationContext.MemberName is null ? null : [validationContext.MemberName]);
        }

        return ValidationResult.Success;
    }

    private static bool IsCultureName(string name)
    {
        if (!WellFormed().IsMatch(name))
        {
            return false;
        }

        try
        {
            return !CultureInfo.GetCultureInfo(name, predefinedOnly: true).Equals(CultureInfo.InvariantCulture);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
