using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class SubordinatedLoan
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string LoanNumber { get; set; } = string.Empty;
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal InterestRate { get; set; }
    
    public DateTime IssueDate { get; set; }
    
    public DateTime? MaturityDate { get; set; }
    
    public DateTime? EarlyTerminationDate { get; set; }
    
    public LoanStatus Status { get; set; } = LoanStatus.Active;
    
    public int? NoticePeriodDays { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CurrentValue { get; set; }
    
    public string? Terms { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<LoanInterest> InterestPayments { get; set; } = new List<LoanInterest>();
    
    // Computed properties
    public decimal TotalPaid => Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
    
    public decimal OutstandingAmount => Amount - TotalPaid;
    
    public decimal AccruedInterest => CalculateAccruedInterest();
    
    public bool IsFullyPaid => TotalPaid >= Amount;
    
    private decimal CalculateAccruedInterest()
    {
        if (Status != LoanStatus.Active) return 0;
        
        var lastInterestPayment = InterestPayments.Where(i => i.Status == InterestStatus.Paid)
            .OrderByDescending(i => i.PaymentDate)
            .FirstOrDefault();
        
        var startDate = lastInterestPayment?.PaymentDate ?? IssueDate;
        var endDate = DateTime.UtcNow;
        
        if (EarlyTerminationDate.HasValue && EarlyTerminationDate < endDate)
            endDate = EarlyTerminationDate.Value;
        
        var days = (endDate - startDate).Days;
        return (CurrentValue ?? Amount) * InterestRate * days / 365;
    }
}

public class LoanInterest
{
    public int Id { get; set; }
    
    [Required]
    public int SubordinatedLoanId { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal Rate { get; set; }
    
    public DateTime PeriodStart { get; set; }
    
    public DateTime PeriodEnd { get; set; }
    
    public DateTime? PaymentDate { get; set; }
    
    public InterestStatus Status { get; set; } = InterestStatus.Accrued;
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual SubordinatedLoan SubordinatedLoan { get; set; } = null!;
}

public enum LoanStatus
{
    Active,
    Mature,
    Terminated,
    Cancelled
}

public enum InterestStatus
{
    Accrued,
    Due,
    Paid,
    Waived
}