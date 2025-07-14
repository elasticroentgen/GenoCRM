using System.ComponentModel.DataAnnotations;

namespace GenoCRM.Models.Domain;

public class Document
{
    public int Id { get; set; }
    
    [Required]
    public int MemberId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;
    
    [Required]
    public long FileSize { get; set; }
    
    [Required]
    public DocumentType Type { get; set; }
    
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    
    public string? Description { get; set; }
    
    public string? NextcloudPath { get; set; }
    
    public string? NextcloudShareLink { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    public bool IsConfidential { get; set; } = false;
    
    public string? Tags { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(100)]
    public string? CreatedBy { get; set; }
    
    [StringLength(100)]
    public string? UpdatedBy { get; set; }
    
    // Navigation properties
    public virtual Member Member { get; set; } = null!;
    public virtual ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    
    // Computed properties
    public string FileSizeFormatted => FormatFileSize(FileSize);
    
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate < DateTime.UtcNow;
    
    public DocumentVersion? LatestVersion => Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
    
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class DocumentVersion
{
    public int Id { get; set; }
    
    [Required]
    public int DocumentId { get; set; }
    
    [Required]
    public int VersionNumber { get; set; }
    
    [Required]
    [StringLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public long FileSize { get; set; }
    
    public string? NextcloudPath { get; set; }
    
    public string? ChangeDescription { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [StringLength(100)]
    public string? CreatedBy { get; set; }
    
    // Navigation properties
    public virtual Document Document { get; set; } = null!;
}

public enum DocumentType
{
    MembershipApplication,
    ShareCertificate,
    LoanAgreement,
    DividendNotice,
    TaxDocument,
    Correspondence,
    Meeting,
    Legal,
    Financial,
    Other
}

public enum DocumentStatus
{
    Active,
    Archived,
    Deleted,
    Expired
}