using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class AuditLog
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string EntityId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string EntityDescription { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? Permission { get; set; }
    
    [Column(TypeName = "TEXT")]
    public string? Changes { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(45)]
    public string? IpAddress { get; set; }
    
    [StringLength(500)]
    public string? UserAgent { get; set; }
}

public enum AuditAction
{
    Create,
    Update,
    Delete,
    Transfer,
    Approve,
    Cancel,
    Pay,
    Suspend,
    Reactivate
}