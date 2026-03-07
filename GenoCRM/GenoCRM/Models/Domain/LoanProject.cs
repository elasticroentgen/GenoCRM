using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class LoanProject
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(50)]
    public string ProjectNumber { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinancingAmount { get; set; }

    public LoanProjectStatus Status { get; set; } = LoanProjectStatus.Draft;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<LoanOffer> LoanOffers { get; set; } = new List<LoanOffer>();

    // Computed properties
    [NotMapped]
    public decimal TotalSubscribed => LoanOffers
        .SelectMany(o => o.Subscriptions)
        .Where(s => s.Status == LoanSubscriptionStatus.Active || s.Status == LoanSubscriptionStatus.Completed)
        .Sum(s => s.Amount);

    [NotMapped]
    public decimal FinancingProgress => FinancingAmount > 0 ? Math.Round(TotalSubscribed / FinancingAmount * 100, 2) : 0;
}

public enum LoanProjectStatus
{
    Draft,
    Active,
    Closed,
    Cancelled
}
