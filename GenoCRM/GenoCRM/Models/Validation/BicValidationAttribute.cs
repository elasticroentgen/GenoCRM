using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace GenoCRM.Models.Validation;

public partial class BicValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string bic || string.IsNullOrWhiteSpace(bic))
            return ValidationResult.Success; // optional field, skip if empty

        bic = bic.Replace(" ", "").ToUpperInvariant();

        // ISO 9362: 4 letters bank code, 2 letters country code, 2 alphanumeric location,
        // optional 3 alphanumeric branch code → total 8 or 11 chars.
        if (!BicFormatRegex().IsMatch(bic))
            return new ValidationResult(ErrorMessage ?? "Invalid BIC format.");

        return ValidationResult.Success;
    }

    [GeneratedRegex(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex BicFormatRegex();
}
