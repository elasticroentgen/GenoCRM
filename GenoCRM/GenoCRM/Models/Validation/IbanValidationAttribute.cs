using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.RegularExpressions;

namespace GenoCRM.Models.Validation;

public partial class IbanValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string iban || string.IsNullOrWhiteSpace(iban))
            return ValidationResult.Success; // nullable field, skip if empty

        iban = iban.Replace(" ", "").ToUpperInvariant();

        if (!IbanFormatRegex().IsMatch(iban))
            return new ValidationResult(ErrorMessage ?? "Invalid IBAN format.");

        if (iban.Length < 15 || iban.Length > 34)
            return new ValidationResult(ErrorMessage ?? "IBAN must be between 15 and 34 characters.");

        // MOD-97 check (ISO 7064)
        // Move first 4 chars to end, convert letters to digits (A=10..Z=35), check mod 97 == 1
        var rearranged = iban[4..] + iban[..4];
        var numericString = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        if (BigInteger.Parse(numericString) % 97 != 1)
            return new ValidationResult(ErrorMessage ?? "Invalid IBAN check digits.");

        return ValidationResult.Success;
    }

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanFormatRegex();
}
