using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class LoanOffer
{
    public int Id { get; set; }

    [Required]
    public int LoanProjectId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal InterestRate { get; set; }

    [Required]
    public int TermMonths { get; set; }

    public PaymentInterval PaymentInterval { get; set; } = PaymentInterval.Monthly;

    public RepaymentType RepaymentType { get; set; } = RepaymentType.Annuity;

    public int GracePeriodMonths { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinSubscriptionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxSubscriptionAmount { get; set; }

    public LoanOfferStatus Status { get; set; } = LoanOfferStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual LoanProject LoanProject { get; set; } = null!;
    public virtual ICollection<LoanSubscription> Subscriptions { get; set; } = new List<LoanSubscription>();
}

public enum PaymentInterval
{
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual
}

public enum RepaymentType
{
    Annuity
}

public enum LoanOfferStatus
{
    Open,
    Closed,
    Cancelled
}
