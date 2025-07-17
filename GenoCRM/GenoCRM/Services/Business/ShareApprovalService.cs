using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public class ShareApprovalService : IShareApprovalService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<ShareApprovalService> _logger;
    private readonly IShareService _shareService;

    public ShareApprovalService(GenoDbContext context, ILogger<ShareApprovalService> logger, IShareService shareService)
    {
        _context = context;
        _logger = logger;
        _shareService = shareService;
    }

    public async Task<ShareApproval> CreateShareApprovalRequestAsync(int memberId, int requestedQuantity)
    {
        try
        {
            if (!await CanRequestAdditionalSharesAsync(memberId, requestedQuantity))
            {
                throw new InvalidOperationException("Additional shares request is not allowed");
            }

            var shareDenomination = 250.00m; // Fixed denomination as per Satzung
            var totalValue = requestedQuantity * shareDenomination;

            var approval = new ShareApproval
            {
                MemberId = memberId,
                RequestedQuantity = requestedQuantity,
                TotalValue = totalValue,
                Status = ShareApprovalStatus.Pending,
                RequestDate = DateTime.UtcNow
            };

            _context.ShareApprovals.Add(approval);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Share approval request created: {ApprovalId} for member {MemberId} requesting {Quantity} shares", 
                approval.Id, memberId, requestedQuantity);

            return approval;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share approval request for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<bool> ApproveShareRequestAsync(int approvalId, string approvedBy)
    {
        try
        {
            var approval = await _context.ShareApprovals
                .Include(a => a.Member)
                .FirstOrDefaultAsync(a => a.Id == approvalId);

            if (approval == null || approval.Status != ShareApprovalStatus.Pending)
            {
                return false;
            }

            // Re-validate the request (excluding the current approval being processed)
            if (!await CanRequestAdditionalSharesAsync(approval.MemberId, approval.RequestedQuantity, approvalId))
            {
                return false;
            }

            approval.Status = ShareApprovalStatus.Approved;
            approval.ApprovalDate = DateTime.UtcNow;
            approval.ApprovedBy = approvedBy;
            approval.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share approval approved: {ApprovalId} by {ApprovedBy}", approvalId, approvedBy);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving share request {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task<bool> RejectShareRequestAsync(int approvalId, string rejectedBy, string reason)
    {
        try
        {
            var approval = await _context.ShareApprovals.FindAsync(approvalId);
            if (approval == null || approval.Status != ShareApprovalStatus.Pending)
            {
                return false;
            }

            approval.Status = ShareApprovalStatus.Rejected;
            approval.ApprovalDate = DateTime.UtcNow;
            approval.ApprovedBy = rejectedBy;
            approval.RejectionReason = reason;
            approval.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share approval rejected: {ApprovalId} by {RejectedBy} - {Reason}", 
                approvalId, rejectedBy, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting share request {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task<bool> CompleteShareApprovalAsync(int approvalId)
    {
        const int maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var approval = await _context.ShareApprovals
                    .Include(a => a.Member)
                    .FirstOrDefaultAsync(a => a.Id == approvalId);

                if (approval == null || approval.Status != ShareApprovalStatus.Approved)
                {
                    return false;
                }

                // Create the new shares
                var newCertificateNumber = await _shareService.GenerateNextCertificateNumberAsync();
                var newShare = new CooperativeShare
                {
                    MemberId = approval.MemberId,
                    CertificateNumber = newCertificateNumber,
                    Quantity = approval.RequestedQuantity,
                    NominalValue = 250.00m, // Fixed denomination as per Satzung
                    Value = 250.00m, // Fixed denomination as per Satzung
                    IssueDate = DateTime.UtcNow,
                    Status = ShareStatus.Active
                };

                _context.CooperativeShares.Add(newShare);

                approval.Status = ShareApprovalStatus.Completed;
                approval.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Share approval completed: {ApprovalId}", approvalId);

                return true;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
                ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx &&
                sqliteEx.SqliteErrorCode == 19 && // SQLITE_CONSTRAINT
                sqliteEx.Message.Contains("UNIQUE constraint failed: CooperativeShares.CertificateNumber") &&
                attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Certificate number collision detected on attempt {Attempt}, retrying share approval completion for {ApprovalId}", 
                    attempt + 1, approvalId);
                
                // Reset the context to clear any tracked entities
                _context.ChangeTracker.Clear();
                
                // Wait a bit before retrying
                await Task.Delay(100 * (attempt + 1));
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing share approval {ApprovalId} on attempt {Attempt}", approvalId, attempt + 1);
                throw;
            }
        }
        
        throw new InvalidOperationException($"Failed to complete share approval {approvalId} after {maxRetries} attempts due to certificate number conflicts");
    }

    public async Task<ShareApproval?> GetShareApprovalByIdAsync(int id)
    {
        try
        {
            return await _context.ShareApprovals
                .Include(a => a.Member)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share approval {ApprovalId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ShareApproval>> GetShareApprovalsByMemberAsync(int memberId)
    {
        try
        {
            return await _context.ShareApprovals
                .Include(a => a.Member)
                .Where(a => a.MemberId == memberId)
                .OrderByDescending(a => a.RequestDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share approvals for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<ShareApproval>> GetPendingShareApprovalsAsync()
    {
        try
        {
            return await _context.ShareApprovals
                .Include(a => a.Member)
                .Where(a => a.Status == ShareApprovalStatus.Pending)
                .OrderBy(a => a.RequestDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending share approvals");
            throw;
        }
    }

    public async Task<bool> CanRequestAdditionalSharesAsync(int memberId, int requestedQuantity, int? excludeApprovalId = null)
    {
        try
        {
            if (requestedQuantity <= 0)
            {
                return false;
            }

            // Check if member exists and is active
            var member = await _context.Members.FindAsync(memberId);
            if (member == null || member.Status != MemberStatus.Active)
            {
                return false;
            }

            // Check if member has completed initial share purchase
            if (!await HasMemberCompletedInitialShareAsync(memberId))
            {
                return false;
            }

            // Check if there are any pending approval requests (excluding the one being processed)
            var query = _context.ShareApprovals
                .Where(a => a.MemberId == memberId && a.Status == ShareApprovalStatus.Pending);

            if (excludeApprovalId.HasValue)
            {
                query = query.Where(a => a.Id != excludeApprovalId.Value);
            }

            var pendingApprovals = await query.CountAsync();

            if (pendingApprovals > 0)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if member {MemberId} can request additional shares", memberId);
            throw;
        }
    }

    public async Task<bool> HasMemberCompletedInitialShareAsync(int memberId)
    {
        try
        {
            var initialShare = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Where(s => s.MemberId == memberId && s.Status == ShareStatus.Active)
                .FirstOrDefaultAsync();

            return initialShare != null && initialShare.IsFullyPaid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if member {MemberId} has completed initial share", memberId);
            throw;
        }
    }
}