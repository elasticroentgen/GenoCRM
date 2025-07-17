using System.ComponentModel.DataAnnotations;

namespace GenoCRM.Models.Domain;

public class ShareApproval
{
    public int Id { get; set; }
    
    public int MemberId { get; set; }
    public virtual Member Member { get; set; } = null!;
    
    public int RequestedQuantity { get; set; }
    
    public decimal TotalValue { get; set; }
    
    public ShareApprovalStatus Status { get; set; } = ShareApprovalStatus.Pending;
    
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? ApprovalDate { get; set; }
    
    public string? ApprovedBy { get; set; }
    
    public string? RejectionReason { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ShareApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Completed
}