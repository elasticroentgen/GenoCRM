using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class Payment
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    public int? ShareId { get; set; }
    
    public int? SubordinatedLoanId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    public PaymentType Type { get; set; }
    
    [Required]
    public PaymentMethod Method { get; set; }
    
    public DateTime PaymentDate { get; set; }
    
    public DateTime? ProcessedDate { get; set; }
    
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    
    [StringLength(100)]
    public string? Reference { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual CooperativeShare? Share { get; set; }
    public virtual SubordinatedLoan? SubordinatedLoan { get; set; }
}

public enum PaymentType
{
    ShareCapital,
    SubordinatedLoan,
    Refund,
    Fee,
    Other
}

public enum PaymentMethod
{
    BankTransfer,
    Cash,
    Check,
    CreditCard,
    DebitCard,
    Other
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
    Refunded
}