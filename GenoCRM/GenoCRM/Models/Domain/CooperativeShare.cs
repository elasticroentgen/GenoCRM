using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class CooperativeShare
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string CertificateNumber { get; set; } = string.Empty;
    
    [Required]
    public int Quantity { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal NominalValue { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; }
    
    public DateTime IssueDate { get; set; }
    
    public DateTime? CancellationDate { get; set; }
    
    public ShareStatus Status { get; set; } = ShareStatus.Active;
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Dividend> Dividends { get; set; } = new List<Dividend>();
    
    // Computed properties
    public decimal TotalValue => Quantity * Value;
    
    public bool IsFullyPaid => Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount) >= TotalValue;
    
    public decimal PaidAmount => Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
    
    public decimal OutstandingAmount => TotalValue - PaidAmount;
}

public enum ShareStatus
{
    Active,
    Cancelled,
    Transferred,
    Suspended
}