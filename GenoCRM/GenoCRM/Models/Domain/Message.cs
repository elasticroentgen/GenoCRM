using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenoCRM.Models.Domain;

public class Message
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    public int? UserId { get; set; } // Who sent the message
    
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [Required]
    public MessageType Type { get; set; }
    
    [Required]
    public MessageChannel Channel { get; set; }
    
    [Required]
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    
    [StringLength(20)]
    public string? ExternalId { get; set; } // For tracking with external providers
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal? Cost { get; set; } // For SMS/WhatsApp billing
    
    public DateTime? SentAt { get; set; }
    
    public DateTime? DeliveredAt { get; set; }
    
    public DateTime? ReadAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public int RetryCount { get; set; } = 0;
    
    public DateTime? NextRetryAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual MessageTemplate? Template { get; set; }
    
    // Computed properties
    public bool IsDelivered => Status == MessageStatus.Delivered;
    public bool IsRead => ReadAt.HasValue;
    public bool HasFailed => Status == MessageStatus.Failed;
}

public class MessageTemplate
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [Required]
    public MessageType Type { get; set; }
    
    [Required]
    public MessageChannel Channel { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public string? Description { get; set; }
    
    // Template variables (JSON string)
    public string? Variables { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class MessagePreference
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    [Required]
    public MessageType Type { get; set; }
    
    [Required]
    public MessageChannel PreferredChannel { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
}

public class MessageCampaign
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [Required]
    public MessageType Type { get; set; }
    
    [Required]
    public MessageChannel Channel { get; set; }
    
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    
    // Member filtering criteria (JSON string)
    public string? MemberFilter { get; set; }
    
    public DateTime? ScheduledAt { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public int TotalMembers { get; set; }
    
    public int MessagesSent { get; set; }
    
    public int MessagesDelivered { get; set; }
    
    public int MessagesFailed { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

public enum MessageType
{
    PaymentReminder,
    ShareTransferNotification,
    DividendNotification,
    GeneralAssemblyNotice,
    MemberExclusionWarning,
    ShareCancellationConfirmation,
    WelcomeMessage,
    OffboardingNotification,
    PaymentConfirmation,
    AnnualReportDistribution,
    GeneralBroadcast,
    SystemNotification,
    Other
}

public enum MessageChannel
{
    Email,
    WhatsApp,
    SMS,
    Push // For future mobile app integration
}

public enum MessageStatus
{
    Pending,
    Queued,
    Sending,
    Sent,
    Delivered,
    Read,
    Failed,
    Cancelled
}

public enum CampaignStatus
{
    Draft,
    Scheduled,
    Running,
    Completed,
    Cancelled,
    Failed
}