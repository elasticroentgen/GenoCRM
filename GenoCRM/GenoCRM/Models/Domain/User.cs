using System.ComponentModel.DataAnnotations;

namespace GenoCRM.Models.Domain;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string NextcloudUserId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? DisplayName { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime LastLoginAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    
    // Computed properties
    public string FullName => $"{FirstName} {LastName}".Trim();
    
    public IEnumerable<string> GroupNames => UserGroups.Select(ug => ug.GroupName);
    
    public bool HasPermission(string permission) => 
        UserPermissions.Any(up => up.Permission == permission && up.IsGranted);
    
    public bool IsInGroup(string groupName) => 
        UserGroups.Any(ug => ug.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase));
}

public class UserGroup
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string GroupName { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}

public class UserPermission
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Permission { get; set; } = string.Empty;
    
    public bool IsGranted { get; set; } = true;
    
    [StringLength(100)]
    public string? GrantedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}


// Permission constants
public static class Permissions
{
    // Member management
    public const string ViewMembers = "members.view";
    public const string CreateMembers = "members.create";
    public const string EditMembers = "members.edit";
    public const string DeleteMembers = "members.delete";
    
    // Share management
    public const string ViewShares = "shares.view";
    public const string CreateShares = "shares.create";
    public const string EditShares = "shares.edit";
    public const string CancelShares = "shares.cancel";
    public const string TransferShares = "shares.transfer";
    public const string ApproveShareTransfers = "shares.transfer.approve";
    public const string RejectShareTransfers = "shares.transfer.reject";
    public const string ConsolidateShares = "shares.consolidate";
    
    // Dividend management
    public const string ViewDividends = "dividends.view";
    public const string CalculateDividends = "dividends.calculate";
    public const string ApproveDividends = "dividends.approve";
    public const string PayDividends = "dividends.pay";
    
    // Document management
    public const string ViewDocuments = "documents.view";
    public const string UploadDocuments = "documents.upload";
    public const string DeleteDocuments = "documents.delete";
    public const string ManageDocuments = "documents.manage";
    
    // Loan management
    public const string ViewLoans = "loans.view";
    public const string CreateLoans = "loans.create";
    public const string EditLoans = "loans.edit";
    public const string ApproveLoans = "loans.approve";
    
    // Administration
    public const string ViewUsers = "admin.users.view";
    public const string ManageUsers = "admin.users.manage";
    public const string ManagePermissions = "admin.permissions.manage";
    public const string ViewAuditLogs = "admin.audit.view";
    
    // Reports
    public const string ViewReports = "reports.view";
    public const string ExportData = "reports.export";
    
    // Messaging
    public const string ViewMessages = "messages.view";
    public const string SendMessages = "messages.send";
    public const string ManageMessages = "messages.manage";
    
    public static readonly Dictionary<string, string> PermissionDescriptions = new()
    {
        { ViewMembers, "View member information" },
        { CreateMembers, "Create new members" },
        { EditMembers, "Edit member information" },
        { DeleteMembers, "Delete/terminate members" },
        { ViewShares, "View share information" },
        { CreateShares, "Issue new shares" },
        { EditShares, "Edit share information" },
        { CancelShares, "Cancel shares" },
        { TransferShares, "Transfer shares between members" },
        { ApproveShareTransfers, "Approve share transfer requests" },
        { RejectShareTransfers, "Reject share transfer requests" },
        { ConsolidateShares, "Consolidate multiple share certificates" },
        { ViewDividends, "View dividend information" },
        { CalculateDividends, "Calculate dividends" },
        { ApproveDividends, "Approve dividend payments" },
        { PayDividends, "Process dividend payments" },
        { ViewDocuments, "View documents" },
        { UploadDocuments, "Upload new documents" },
        { DeleteDocuments, "Delete documents" },
        { ManageDocuments, "Full document management" },
        { ViewLoans, "View subordinated loans" },
        { CreateLoans, "Create new loans" },
        { EditLoans, "Edit loan information" },
        { ApproveLoans, "Approve loan applications" },
        { ViewUsers, "View user accounts" },
        { ManageUsers, "Manage user accounts" },
        { ManagePermissions, "Manage user permissions" },
        { ViewAuditLogs, "View audit logs" },
        { ViewReports, "View reports" },
        { ExportData, "Export data to external formats" },
        { ViewMessages, "View messaging history" },
        { SendMessages, "Send messages to members" },
        { ManageMessages, "Manage messaging system" }
    };
}