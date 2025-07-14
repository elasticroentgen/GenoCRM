using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class Dividend
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    public int? ShareId { get; set; }
    
    [Required]
    public int FiscalYear { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal Rate { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseAmount { get; set; }
    
    public DateTime DeclarationDate { get; set; }
    
    public DateTime? PaymentDate { get; set; }
    
    public DividendStatus Status { get; set; } = DividendStatus.Declared;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TaxWithheld { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NetAmount { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual CooperativeShare? Share { get; set; }
    
    // Computed properties
    public decimal CalculatedNetAmount => Amount - (TaxWithheld ?? 0);
}

public enum DividendStatus
{
    Declared,
    Approved,
    Paid,
    Cancelled
}