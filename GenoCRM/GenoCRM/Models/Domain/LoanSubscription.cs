using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GenoCRM.Models.Validation;

namespace GenoCRM.Models.Domain;

public class LoanSubscription
{
    public int Id { get; set; }

    [Required]
    public int LoanOfferId { get; set; }

    [Required]
    public int MemberId { get; set; }

    [StringLength(50)]
    public string SubscriptionNumber { get; set; } = string.Empty;

    public DateTime SubscriptionDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime? PaidInDate { get; set; }

    public LoanSubscriptionStatus Status { get; set; } = LoanSubscriptionStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(200)]
    public string BankAccountHolder { get; set; } = string.Empty;

    [Required]
    [StringLength(34)]
    [IbanValidation]
    public string IBAN { get; set; } = string.Empty;

    [StringLength(11)]
    [BicValidation]
    public string? BIC { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual LoanOffer LoanOffer { get; set; } = null!;
    public virtual Member Member { get; set; } = null!;
    public virtual LoanPaymentPlan? PaymentPlan { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum LoanSubscriptionStatus
{
    Active,
    Completed,
    Cancelled
}
