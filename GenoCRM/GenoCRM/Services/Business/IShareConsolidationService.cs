using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IShareConsolidationService
{
    /// <summary>
    /// Gets all active share certificates for a member that can be consolidated
    /// </summary>
    Task<IEnumerable<CooperativeShare>> GetConsolidatableSharesAsync(int memberId);
    
    /// <summary>
    /// Validates if the selected shares can be consolidated
    /// </summary>
    Task<ShareConsolidationValidationResult> ValidateConsolidationAsync(IEnumerable<int> shareIds);
    
    /// <summary>
    /// Consolidates multiple share certificates into a single certificate
    /// </summary>
    Task<CooperativeShare> ConsolidateSharesAsync(int memberId, IEnumerable<int> shareIds, string? notes = null);
    
    /// <summary>
    /// Gets consolidation preview showing what the result would look like
    /// </summary>
    Task<ShareConsolidationPreview> GetConsolidationPreviewAsync(IEnumerable<int> shareIds);
    
    /// <summary>
    /// Checks if a member has shares that can be consolidated
    /// </summary>
    Task<bool> CanMemberConsolidateSharesAsync(int memberId);
}

public class ShareConsolidationValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ShareConsolidationPreview
{
    public int TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public bool AllFullyPaid { get; set; }
    public List<CooperativeShare> SourceShares { get; set; } = new();
    public List<Payment> AllPayments { get; set; } = new();
    public List<Dividend> AllDividends { get; set; } = new();
}