using System.ComponentModel.DataAnnotations;

namespace GenoCRM.Models.Domain;

public class Member
{
    public int Id { get; set; }
    
    [StringLength(20)]
    public string MemberNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string Street { get; set; } = string.Empty;
    
    [StringLength(10)]
    public string PostalCode { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string City { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;
    
    public DateTime? BirthDate { get; set; }
    
    public DateTime JoinDate { get; set; }
    
    public DateTime? LeaveDate { get; set; }
    
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<CooperativeShare> Shares { get; set; } = new List<CooperativeShare>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Dividend> Dividends { get; set; } = new List<Dividend>();
    public virtual ICollection<SubordinatedLoan> SubordinatedLoans { get; set; } = new List<SubordinatedLoan>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    
    // Computed properties
    public string FullName => $"{FirstName} {LastName}";
    
    public decimal TotalShareValue => Shares.Where(s => s.Status == ShareStatus.Active).Sum(s => s.Value);
    
    public int TotalShareCount => Shares.Where(s => s.Status == ShareStatus.Active).Sum(s => s.Quantity);
}

public enum MemberStatus
{
    Active,
    Inactive,
    Suspended,
    Terminated
}