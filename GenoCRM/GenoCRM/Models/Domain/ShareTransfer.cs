using System.ComponentModel.DataAnnotations;

namespace GenoCRM.Models.Domain;

public class ShareTransfer
{
    public int Id { get; set; }
    
    public int FromMemberId { get; set; }
    public virtual Member FromMember { get; set; } = null!;
    
    public int ToMemberId { get; set; }
    public virtual Member ToMember { get; set; } = null!;
    
    public int ShareId { get; set; }
    public virtual CooperativeShare Share { get; set; } = null!;
    
    public int Quantity { get; set; }
    
    public decimal TotalValue { get; set; }
    
    public ShareTransferStatus Status { get; set; } = ShareTransferStatus.Pending;
    
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? ApprovalDate { get; set; }
    
    public DateTime? CompletionDate { get; set; }
    
    public string? ApprovedBy { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ShareTransferStatus
{
    Pending,
    Approved,
    Rejected,
    Completed,
    Cancelled
}