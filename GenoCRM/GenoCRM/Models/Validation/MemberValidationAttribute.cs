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

        // Validate individual-specific fields
        if (member.MemberType == MemberType.Individual)
        {
            if (string.IsNullOrWhiteSpace(member.FirstName))
            {
                errors.Add("First name is required for individuals.");
            }
            
            if (string.IsNullOrWhiteSpace(member.LastName))
            {
                errors.Add("Last name is required for individuals.");
            }
        }
        // Validate company-specific fields
        else if (member.MemberType == MemberType.Company)
        {
            if (string.IsNullOrWhiteSpace(member.CompanyName))
            {
                errors.Add("Company name is required for companies.");
            }
        }

        if (errors.Any())
        {
            return new ValidationResult(string.Join(" ", errors));
        }

        return ValidationResult.Success;
    }
}