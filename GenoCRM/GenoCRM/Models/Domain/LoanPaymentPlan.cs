using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class LoanPaymentPlan
{
    public int Id { get; set; }

    [Required]
    public int LoanSubscriptionId { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual LoanSubscription LoanSubscription { get; set; } = null!;
    public virtual ICollection<LoanPaymentPlanEntry> Entries { get; set; } = new List<LoanPaymentPlanEntry>();
}

public class LoanPaymentPlanEntry
{
    public int Id { get; set; }

    [Required]
    public int LoanPaymentPlanId { get; set; }

    public int PeriodNumber { get; set; }

    public DateTime DueDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrincipalAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal InterestAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingBalance { get; set; }

    public PaymentPlanEntryStatus Status { get; set; } = PaymentPlanEntryStatus.Pending;

    public DateTime? PaidDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual LoanPaymentPlan LoanPaymentPlan { get; set; } = null!;
}

public enum PaymentPlanEntryStatus
{
    Pending,
    Due,
    Paid,
    Overdue
}
