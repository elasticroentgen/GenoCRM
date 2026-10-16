using System.ComponentModel.DataAnnotations;
using GenoCRM.Models.Domain;

namespace GenoCRM.Models.Validation;

public class MemberValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not Member member)
        {
            return ValidationResult.Success;
        }

        var errors = new List<string>();
        var fields = new List<string>();

        // Validate individual-specific fields
        if (member.MemberType == MemberType.Individual)
        {
            if (string.IsNullOrWhiteSpace(member.FirstName))
            {
                errors.Add("First name is required for individuals.");
                fields.Add(nameof(Member.FirstName));
            }
            
            if (string.IsNullOrWhiteSpace(member.LastName))
            {
                errors.Add("Last name is required for individuals.");
                fields.Add(nameof(Member.LastName));
            }
        }
        // Validate company-specific fields
        else if (member.MemberType == MemberType.Company)
        {
            if (string.IsNullOrWhiteSpace(member.CompanyName))
            {
                errors.Add("Company name is required for companies.");
                fields.Add(nameof(Member.CompanyName));
            }
        }

        if (errors.Any())
        {
            // Name the fields so the form can show the message next to the input, not only in the summary
            return new ValidationResult(string.Join(" ", errors), fields);
        }

        return ValidationResult.Success;
    }
}