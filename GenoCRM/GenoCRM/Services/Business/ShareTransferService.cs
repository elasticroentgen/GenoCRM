using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public class ShareTransferService : IShareTransferService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<ShareTransferService> _logger;
    private readonly IShareService _shareService;
    private readonly IConfiguration _configuration;

    public ShareTransferService(GenoDbContext context, ILogger<ShareTransferService> logger, IShareService shareService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _shareService = shareService;
        _configuration = configuration;
    }

    public async Task<ShareTransfer> CreateShareTransferRequestAsync(int fromMemberId, int toMemberId, int shareId, int quantity)
    {
        try
        {
            if (!await CanTransferSharesAsync(fromMemberId, toMemberId, shareId, quantity))
            {
                throw new InvalidOperationException("Share transfer is not allowed");
            }

            var share = await _context.CooperativeShares.FindAsync(shareId);
            if (share == null)
            {
                throw new ArgumentException("Share not found");
            }

            var transfer = new ShareTransfer
            {
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                ShareId = shareId,
                Quantity = quantity,
                TotalValue = quantity * share.NominalValue,
                Status = ShareTransferStatus.Pending,
                RequestDate = DateTime.UtcNow
            };

            _context.ShareTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Share transfer request created: {TransferId} from member {FromMemberId} to member {ToMemberId}", 
                transfer.Id, fromMemberId, toMemberId);

            return transfer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share transfer request from member {FromMemberId} to member {ToMemberId}", 
                fromMemberId, toMemberId);
            throw;
        }
    }

    public async Task<bool> ApproveShareTransferAsync(int transferId, string approvedBy)
    {
        try
        {
            var transfer = await _context.ShareTransfers
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share)
                .FirstOrDefaultAsync(t => t.Id == transferId);

            if (transfer == null || transfer.Status != ShareTransferStatus.Pending)
            {
                return false;
            }

            // Re-validate the transfer
            if (!await ValidateShareTransferAsync(transfer.FromMemberId, transfer.ToMemberId, transfer.ShareId, transfer.Quantity))
            {
                return false;
            }

            transfer.Status = ShareTransferStatus.Approved;
            transfer.ApprovalDate = DateTime.UtcNow;
            transfer.ApprovedBy = approvedBy;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share transfer approved: {TransferId} by {ApprovedBy}", transferId, approvedBy);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving share transfer {TransferId}", transferId);
            throw;
        }
    }

    public async Task<bool> RejectShareTransferAsync(int transferId, string rejectedBy, string reason)
    {
        try
        {
            var transfer = await _context.ShareTransfers.FindAsync(transferId);
            if (transfer == null || transfer.Status != ShareTransferStatus.Pending)
            {
                return false;
            }

            transfer.Status = ShareTransferStatus.Rejected;
            transfer.ApprovalDate = DateTime.UtcNow;
            transfer.ApprovedBy = rejectedBy;
            transfer.Notes = reason;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share transfer rejected: {TransferId} by {RejectedBy} - {Reason}", 
                transferId, rejectedBy, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting share transfer {TransferId}", transferId);
            throw;
        }
    }

    public async Task<bool> CompleteShareTransferAsync(int transferId)
    {
        const int maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var transfer = await _context.ShareTransfers
                    .Include(t => t.Share)
                    .FirstOrDefaultAsync(t => t.Id == transferId);

                if (transfer == null || transfer.Status != ShareTransferStatus.Approved)
                {
                    return false;
                }

                // Validate max shares per member limit for the recipient
                var currentActiveShares = await _context.CooperativeShares
                    .Where(s => s.MemberId == transfer.ToMemberId && s.Status == ShareStatus.Active)
                    .SumAsync(s => s.Quantity);
                
                var maxSharesPerMember = _configuration.GetValue<int>("CooperativeSettings:MaxSharesPerMember");
                if (maxSharesPerMember <= 0) maxSharesPerMember = 100; // Default fallback
                
                if (currentActiveShares + transfer.Quantity > maxSharesPerMember)
                {
                    throw new InvalidOperationException($"Transferring {transfer.Quantity} shares would exceed the maximum allowed shares per member ({maxSharesPerMember}). Recipient currently has {currentActiveShares} active shares.");
                }

                // Create new share for the recipient
                var newCertificateNumber = await _shareService.GenerateNextCertificateNumberAsync();
                var newShare = new CooperativeShare
                {
                    MemberId = transfer.ToMemberId,
                    CertificateNumber = newCertificateNumber,
                    Quantity = transfer.Quantity,
                    NominalValue = transfer.Share.NominalValue,
                    Value = transfer.Share.Value,
                    IssueDate = DateTime.UtcNow,
                    Status = ShareStatus.Active
                };

                _context.CooperativeShares.Add(newShare);

                // Update or reduce the original share
                var originalShare = transfer.Share;
                if (originalShare.Quantity == transfer.Quantity)
                {
                    // Transfer entire share
                    originalShare.Status = ShareStatus.Transferred;
                }
                else
                {
                    // Reduce original share quantity
                    originalShare.Quantity -= transfer.Quantity;
                }

                transfer.Status = ShareTransferStatus.Completed;
                transfer.CompletionDate = DateTime.UtcNow;
                transfer.UpdatedAt = DateTime.UtcNow;

                // Check if the member (from member) has 0 shares remaining and lock their account
                await CheckAndLockMemberIfNoSharesAsync(transfer.FromMemberId);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Share transfer completed: {TransferId}", transferId);

                return true;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
                ex.InnerException is Npgsql.PostgresException pgEx &&
                pgEx.SqlState == "23505" && // unique_violation
                pgEx.Message.Contains("duplicate key value violates unique constraint") &&
                attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Certificate number collision detected on attempt {Attempt}, retrying share transfer completion for {TransferId}", 
                    attempt + 1, transferId);
                
                // Reset the context to clear any tracked entities
                _context.ChangeTracker.Clear();
                
                // Wait a bit before retrying
                await Task.Delay(100 * (attempt + 1));
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing share transfer {TransferId} on attempt {Attempt}", transferId, attempt + 1);
                throw;
            }
        }
        
        throw new InvalidOperationException($"Failed to complete share transfer {transferId} after {maxRetries} attempts due to certificate number conflicts");
    }

    public async Task<bool> CancelShareTransferAsync(int transferId)
    {
        try
        {
            var transfer = await _context.ShareTransfers.FindAsync(transferId);
            if (transfer == null || transfer.Status == ShareTransferStatus.Completed)
            {
                return false;
            }

            transfer.Status = ShareTransferStatus.Cancelled;
            transfer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share transfer cancelled: {TransferId}", transferId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling share transfer {TransferId}", transferId);
            throw;
        }
    }

    public async Task<ShareTransfer?> GetShareTransferByIdAsync(int id)
    {
        try
        {
            return await _context.ShareTransfers
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share)
                .FirstOrDefaultAsync(t => t.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share transfer {TransferId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ShareTransfer>> GetShareTransfersByMemberAsync(int memberId)
    {
        try
        {
            return await _context.ShareTransfers
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share)
                .Where(t => t.FromMemberId == memberId || t.ToMemberId == memberId)
                .OrderByDescending(t => t.RequestDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share transfers for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<ShareTransfer>> GetAllShareTransfersAsync()
    {
        try
        {
            return await _context.ShareTransfers
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share)
                .OrderByDescending(t => t.RequestDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all share transfers");
            throw;
        }
    }

    public async Task<IEnumerable<ShareTransfer>> GetPendingShareTransfersAsync()
    {
        try
        {
            return await _context.ShareTransfers
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share)
                .Where(t => t.Status == ShareTransferStatus.Pending)
                .OrderBy(t => t.RequestDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending share transfers");
            throw;
        }
    }

    public async Task<bool> CanTransferSharesAsync(int fromMemberId, int toMemberId, int shareId, int quantity)
    {
        try
        {
            return await ValidateShareTransferAsync(fromMemberId, toMemberId, shareId, quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if shares can be transferred");
            throw;
        }
    }

    public async Task<bool> ValidateShareTransferAsync(int fromMemberId, int toMemberId, int shareId, int quantity)
    {
        try
        {
            if (fromMemberId == toMemberId)
            {
                return false;
            }

            if (quantity <= 0)
            {
                return false;
            }

            // Check if from member exists and is active
            var fromMember = await _context.Members.FindAsync(fromMemberId);
            if (fromMember == null || fromMember.Status != MemberStatus.Active)
            {
                return false;
            }

            // Check if to member exists and is active
            var toMember = await _context.Members.FindAsync(toMemberId);
            if (toMember == null || toMember.Status != MemberStatus.Active)
            {
                return false;
            }

            // Check if share exists and belongs to from member
            var share = await _context.CooperativeShares.FindAsync(shareId);
            if (share == null || share.MemberId != fromMemberId || share.Status != ShareStatus.Active)
            {
                return false;
            }

            // Check if sufficient quantity available
            if (share.Quantity < quantity)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating share transfer");
            throw;
        }
    }

    private async Task CheckAndLockMemberIfNoSharesAsync(int memberId)
    {
        try
        {
            var member = await _context.Members
                .Include(m => m.Shares)
                .FirstOrDefaultAsync(m => m.Id == memberId);

            if (member == null)
            {
                _logger.LogWarning("Member with ID {MemberId} not found when checking for share lock", memberId);
                return;
            }

            // Count active shares for this member
            var activeShareCount = member.Shares
                .Where(s => s.Status == ShareStatus.Active)
                .Sum(s => s.Quantity);

            // If member has 0 active shares and is currently active, lock their account
            if (activeShareCount == 0 && member.Status == MemberStatus.Active)
            {
                member.Status = MemberStatus.Locked;
                member.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Member {MemberId} ({MemberNumber}) has been locked due to having 0 active shares", 
                    member.Id, member.MemberNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and locking member {MemberId} for zero shares", memberId);
            throw;
        }
    }
}