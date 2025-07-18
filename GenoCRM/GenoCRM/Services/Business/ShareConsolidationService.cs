using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using System.Security.Claims;

namespace GenoCRM.Services.Business;

public class ShareConsolidationService : IShareConsolidationService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<ShareConsolidationService> _logger;
    private readonly IShareService _shareService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ShareConsolidationService(
        GenoDbContext context, 
        ILogger<ShareConsolidationService> logger,
        IShareService shareService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _shareService = shareService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IEnumerable<CooperativeShare>> GetConsolidatableSharesAsync(int memberId)
    {
        try
        {
            var shares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Include(s => s.Dividends)
                .Where(s => s.MemberId == memberId && 
                           s.Status == ShareStatus.Active &&
                           s.CancellationDate == null)
                .OrderBy(s => s.IssueDate)
                .ToListAsync();

            // Only return fully paid shares
            return shares.Where(s => s.IsFullyPaid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consolidatable shares for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<ShareConsolidationValidationResult> ValidateConsolidationAsync(IEnumerable<int> shareIds)
    {
        var result = new ShareConsolidationValidationResult();

        try
        {
            var shareIdList = shareIds.ToList();
            
            if (shareIdList.Count < 2)
            {
                result.ErrorMessage = "At least two shares must be selected for consolidation";
                return result;
            }

            var shares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Where(s => shareIdList.Contains(s.Id))
                .ToListAsync();

            if (shares.Count != shareIdList.Count)
            {
                result.ErrorMessage = "One or more selected shares were not found";
                return result;
            }

            // Validate all shares belong to the same member
            var memberIds = shares.Select(s => s.MemberId).Distinct().ToList();
            if (memberIds.Count > 1)
            {
                result.ErrorMessage = "All shares must belong to the same member";
                return result;
            }

            // Validate all shares are active
            var inactiveShares = shares.Where(s => s.Status != ShareStatus.Active).ToList();
            if (inactiveShares.Any())
            {
                result.ErrorMessage = $"Cannot consolidate non-active shares: {string.Join(", ", inactiveShares.Select(s => s.CertificateNumber))}";
                return result;
            }

            // Validate no shares are scheduled for cancellation
            var scheduledForCancellation = shares.Where(s => s.CancellationDate.HasValue).ToList();
            if (scheduledForCancellation.Any())
            {
                result.ErrorMessage = $"Cannot consolidate shares scheduled for cancellation: {string.Join(", ", scheduledForCancellation.Select(s => s.CertificateNumber))}";
                return result;
            }

            // Validate all shares have the same nominal and current value
            var nominalValues = shares.Select(s => s.NominalValue).Distinct().ToList();
            var currentValues = shares.Select(s => s.Value).Distinct().ToList();
            
            if (nominalValues.Count > 1)
            {
                result.ErrorMessage = "All shares must have the same nominal value";
                return result;
            }

            if (currentValues.Count > 1)
            {
                result.ErrorMessage = "All shares must have the same current value";
                return result;
            }

            // Validate all shares are fully paid
            var partiallyPaidShares = shares.Where(s => !s.IsFullyPaid).ToList();
            if (partiallyPaidShares.Any())
            {
                result.ErrorMessage = $"Cannot consolidate shares that are not fully paid: {string.Join(", ", partiallyPaidShares.Select(s => s.CertificateNumber))}";
                return result;
            }

            // Check for pending transfers
            var pendingTransfers = await _context.ShareTransfers
                .Where(st => shareIdList.Contains(st.ShareId) && 
                            st.Status == ShareTransferStatus.Pending)
                .ToListAsync();

            if (pendingTransfers.Any())
            {
                result.ErrorMessage = "Cannot consolidate shares with pending transfers";
                return result;
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating share consolidation for shares {ShareIds}", string.Join(",", shareIds));
            result.ErrorMessage = "An error occurred while validating the consolidation";
            return result;
        }
    }

    public async Task<CooperativeShare> ConsolidateSharesAsync(int memberId, IEnumerable<int> shareIds, string? notes = null)
    {
        var shareIdList = shareIds.ToList();
        
        // Validate before proceeding
        var validation = await ValidateConsolidationAsync(shareIdList);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Consolidation validation failed: {validation.ErrorMessage}");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Get the shares to consolidate
            var shares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Include(s => s.Dividends)
                .Where(s => shareIdList.Contains(s.Id))
                .OrderBy(s => s.IssueDate)
                .ToListAsync();

            var oldestShare = shares.First();
            var totalQuantity = shares.Sum(s => s.Quantity);
            
            // Create new consolidated certificate
            var consolidatedShare = new CooperativeShare
            {
                MemberId = memberId,
                CertificateNumber = await _shareService.GenerateNextCertificateNumberAsync(),
                Quantity = totalQuantity,
                NominalValue = oldestShare.NominalValue,
                Value = oldestShare.Value,
                IssueDate = DateTime.UtcNow,
                Status = ShareStatus.Active,
                Notes = string.IsNullOrEmpty(notes) 
                    ? $"Consolidated from certificates: {string.Join(", ", shares.Select(s => s.CertificateNumber))}"
                    : $"Consolidated from certificates: {string.Join(", ", shares.Select(s => s.CertificateNumber))} - {notes}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CooperativeShares.Add(consolidatedShare);
            await _context.SaveChangesAsync();

            // Transfer all payments to the new certificate
            var allPayments = shares.SelectMany(s => s.Payments).ToList();
            foreach (var payment in allPayments)
            {
                payment.ShareId = consolidatedShare.Id;
                payment.UpdatedAt = DateTime.UtcNow;
            }

            // Transfer all dividends to the new certificate  
            var allDividends = shares.SelectMany(s => s.Dividends).ToList();
            foreach (var dividend in allDividends)
            {
                dividend.ShareId = consolidatedShare.Id;
                dividend.UpdatedAt = DateTime.UtcNow;
            }

            // Mark old shares as transferred (they're consolidated into the new one)
            foreach (var share in shares)
            {
                share.Status = ShareStatus.Transferred;
                share.Notes = string.IsNullOrEmpty(share.Notes) 
                    ? $"Consolidated into certificate {consolidatedShare.CertificateNumber}"
                    : $"{share.Notes} - Consolidated into certificate {consolidatedShare.CertificateNumber}";
                share.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Log audit trail for each consolidated share
            var consolidationDetails = new 
            {
                ConsolidatedInto = consolidatedShare.CertificateNumber,
                TotalQuantity = totalQuantity,
                SourceCertificates = shares.Select(s => new { s.CertificateNumber, s.Quantity }).ToList()
            };

            foreach (var share in shares)
            {
                await AuditHelper.LogAuditAsync(
                    _auditService,
                    _httpContextAccessor,
                    AuditAction.Transfer,
                    nameof(CooperativeShare),
                    share.Id.ToString(),
                    $"Certificate {share.CertificateNumber} (consolidated)",
                    Permissions.TransferShares,
                    consolidationDetails);
            }

            // Log creation of new consolidated share
            await AuditHelper.LogAuditAsync(
                _auditService,
                _httpContextAccessor,
                AuditAction.Create,
                nameof(CooperativeShare),
                consolidatedShare.Id.ToString(),
                AuditHelper.GetShareDescription(consolidatedShare),
                Permissions.CreateShares,
                consolidationDetails);

            _logger.LogInformation("Successfully consolidated {ShareCount} shares into certificate {CertificateNumber} for member {MemberId}", 
                shares.Count, consolidatedShare.CertificateNumber, memberId);

            return consolidatedShare;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error consolidating shares {ShareIds} for member {MemberId}", 
                string.Join(",", shareIdList), memberId);
            throw;
        }
    }

    public async Task<ShareConsolidationPreview> GetConsolidationPreviewAsync(IEnumerable<int> shareIds)
    {
        try
        {
            var shares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Include(s => s.Dividends)
                .Where(s => shareIds.Contains(s.Id))
                .ToListAsync();

            var allPayments = shares.SelectMany(s => s.Payments).ToList();
            var allDividends = shares.SelectMany(s => s.Dividends).ToList();

            return new ShareConsolidationPreview
            {
                TotalQuantity = shares.Sum(s => s.Quantity),
                TotalValue = shares.Sum(s => s.TotalValue),
                TotalPaidAmount = shares.Sum(s => s.PaidAmount),
                TotalOutstandingAmount = shares.Sum(s => s.OutstandingAmount),
                AllFullyPaid = shares.All(s => s.IsFullyPaid),
                SourceShares = shares,
                AllPayments = allPayments,
                AllDividends = allDividends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consolidation preview for shares {ShareIds}", string.Join(",", shareIds));
            throw;
        }
    }

    public async Task<bool> CanMemberConsolidateSharesAsync(int memberId)
    {
        try
        {
            var consolidatableShares = await GetConsolidatableSharesAsync(memberId);
            return consolidatableShares.Count() >= 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if member {MemberId} can consolidate shares", memberId);
            throw;
        }
    }
}