using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IShareService
{
    Task<IEnumerable<CooperativeShare>> GetAllSharesAsync();
    Task<CooperativeShare?> GetShareByIdAsync(int id);
    Task<IEnumerable<CooperativeShare>> GetSharesByMemberIdAsync(int memberId);
    Task<CooperativeShare> CreateShareAsync(CooperativeShare share);
    Task<CooperativeShare> UpdateShareAsync(CooperativeShare share);
    Task<bool> DeleteShareAsync(int id);
    Task<bool> CertificateNumberExistsAsync(string certificateNumber);
    Task<string> GenerateNextCertificateNumberAsync();
    Task<decimal> GetTotalShareCapitalAsync();
    Task<IEnumerable<CooperativeShare>> GetSharesByStatusAsync(ShareStatus status);
    Task<decimal> GetMemberShareValueAsync(int memberId);
    Task<bool> TransferShareAsync(int shareId, int newMemberId);
    Task<IEnumerable<CooperativeShare>> GetActiveSharesAsync();
    Task<IEnumerable<CooperativeShare>> GetNonActiveSharesAsync();
    Task<decimal> GetOffboardingSharesValueAsync();
    Task<decimal> GetActiveShareCapitalAsync();
    Task<decimal> GetUnpaidShareCapitalAsync();
}

public class ShareService : IShareService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<ShareService> _logger;
    private readonly IConfiguration _configuration;

    public ShareService(GenoDbContext context, ILogger<ShareService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IEnumerable<CooperativeShare>> GetAllSharesAsync()
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .OrderBy(s => s.CertificateNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all shares");
            throw;
        }
    }

    public async Task<CooperativeShare?> GetShareByIdAsync(int id)
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .Include(s => s.Dividends)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving share with ID {ShareId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<CooperativeShare>> GetSharesByMemberIdAsync(int memberId)
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Payments)
                .Include(s => s.Dividends)
                .Where(s => s.MemberId == memberId)
                .OrderBy(s => s.CertificateNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shares for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<CooperativeShare> CreateShareAsync(CooperativeShare share)
    {
        try
        {
            if (await CertificateNumberExistsAsync(share.CertificateNumber))
            {
                throw new InvalidOperationException($"Certificate number {share.CertificateNumber} already exists");
            }

            if (string.IsNullOrEmpty(share.CertificateNumber))
            {
                share.CertificateNumber = await GenerateNextCertificateNumberAsync();
            }

            // Validate member exists
            var memberExists = await _context.Members.AnyAsync(m => m.Id == share.MemberId);
            if (!memberExists)
            {
                throw new InvalidOperationException($"Member with ID {share.MemberId} not found");
            }

            // Validate max shares per member limit
            var currentActiveShares = await _context.CooperativeShares
                .Where(s => s.MemberId == share.MemberId && s.Status == ShareStatus.Active)
                .SumAsync(s => s.Quantity);
            
            var maxSharesPerMember = _configuration.GetValue<int>("CooperativeSettings:MaxSharesPerMember");
            if (maxSharesPerMember <= 0) maxSharesPerMember = 100; // Default fallback
            
            if (currentActiveShares + share.Quantity > maxSharesPerMember)
            {
                throw new InvalidOperationException($"Adding {share.Quantity} shares would exceed the maximum allowed shares per member ({maxSharesPerMember}). Member currently has {currentActiveShares} active shares.");
            }

            share.CreatedAt = DateTime.UtcNow;
            share.UpdatedAt = DateTime.UtcNow;

            _context.CooperativeShares.Add(share);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Share created with ID {ShareId} and certificate number {CertificateNumber}", 
                share.Id, share.CertificateNumber);

            return share;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share");
            throw;
        }
    }

    public async Task<CooperativeShare> UpdateShareAsync(CooperativeShare share)
    {
        try
        {
            var existingShare = await _context.CooperativeShares.FindAsync(share.Id);
            if (existingShare == null)
            {
                throw new InvalidOperationException($"Share with ID {share.Id} not found");
            }

            // Check if certificate number changed and if it's already taken
            if (existingShare.CertificateNumber != share.CertificateNumber)
            {
                if (await CertificateNumberExistsAsync(share.CertificateNumber))
                {
                    throw new InvalidOperationException($"Certificate number {share.CertificateNumber} already exists");
                }
            }

            // Update properties
            existingShare.CertificateNumber = share.CertificateNumber;
            existingShare.Quantity = share.Quantity;
            existingShare.NominalValue = share.NominalValue;
            existingShare.Value = share.Value;
            existingShare.Status = share.Status;
            existingShare.CancellationDate = share.CancellationDate;
            existingShare.Notes = share.Notes;
            existingShare.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share updated with ID {ShareId}", share.Id);

            return existingShare;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating share with ID {ShareId}", share.Id);
            throw;
        }
    }

    public async Task<bool> DeleteShareAsync(int id)
    {
        try
        {
            var share = await _context.CooperativeShares.FindAsync(id);
            if (share == null)
            {
                return false;
            }

            // Soft delete - mark as cancelled
            share.Status = ShareStatus.Cancelled;
            share.CancellationDate = DateTime.UtcNow;
            share.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share soft deleted with ID {ShareId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting share with ID {ShareId}", id);
            throw;
        }
    }

    public async Task<bool> CertificateNumberExistsAsync(string certificateNumber)
    {
        return await _context.CooperativeShares.AnyAsync(s => s.CertificateNumber == certificateNumber);
    }

    public async Task<string> GenerateNextCertificateNumberAsync()
    {
        // For in-memory databases, use simplified approach without transaction complexity
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;
        
        if (isInMemory)
        {
            return await GenerateNextCertificateNumberInternalAsync();
        }
        
        // For real databases, use the robust retry mechanism
        const int maxRetries = 10;
        const int baseDelayMs = 10;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var currentTransaction = _context.Database.CurrentTransaction;
                
                if (currentTransaction == null)
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    try
                    {
                        var result = await GenerateNextCertificateNumberInternalAsync();
                        
                        // Check if this certificate number already exists (double-check)
                        var exists = await _context.CooperativeShares
                            .AnyAsync(s => s.CertificateNumber == result);
                        
                        if (!exists)
                        {
                            await transaction.CommitAsync();
                            return result;
                        }
                        
                        // If it exists, rollback and retry
                        await transaction.RollbackAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                else
                {
                    // We're already in a transaction, just generate
                    var result = await GenerateNextCertificateNumberInternalAsync();
                    
                    // Check if this certificate number already exists (double-check)
                    var exists = await _context.CooperativeShares
                        .AnyAsync(s => s.CertificateNumber == result);
                    
                    if (!exists)
                    {
                        return result;
                    }
                }
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed to generate certificate number, retrying...", attempt + 1);
                
                // Exponential backoff with jitter
                var delay = baseDelayMs * (int)Math.Pow(2, attempt) + new Random().Next(0, 10);
                await Task.Delay(delay);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating next certificate number after {MaxRetries} attempts", maxRetries);
                throw;
            }
        }
        
        throw new InvalidOperationException($"Failed to generate unique certificate number after {maxRetries} attempts");
    }
    
    private async Task<string> GenerateNextCertificateNumberInternalAsync()
    {
        // Get the highest numeric certificate number by parsing all certificate numbers
        var allShares = await _context.CooperativeShares
            .Select(s => s.CertificateNumber)
            .ToListAsync();

        int highestNumber = 0;
        foreach (var certNumber in allShares)
        {
            if (certNumber.StartsWith("CERT") && certNumber.Length >= 7)
            {
                var numberPart = certNumber.Substring(4);
                if (int.TryParse(numberPart, out int number))
                {
                    if (number > highestNumber)
                    {
                        highestNumber = number;
                    }
                }
            }
        }

        var nextNumber = highestNumber + 1;
        return $"CERT{nextNumber:D3}";
    }

    public async Task<decimal> GetTotalShareCapitalAsync()
    {
        try
        {
            return await _context.CooperativeShares
                .Where(s => s.Status == ShareStatus.Active)
                .SumAsync(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total share capital");
            throw;
        }
    }

    public async Task<IEnumerable<CooperativeShare>> GetSharesByStatusAsync(ShareStatus status)
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Member)
                .Where(s => s.Status == status)
                .OrderBy(s => s.CertificateNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shares by status {Status}", status);
            throw;
        }
    }

    public async Task<decimal> GetMemberShareValueAsync(int memberId)
    {
        try
        {
            return await _context.CooperativeShares
                .Where(s => s.MemberId == memberId && s.Status == ShareStatus.Active)
                .SumAsync(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating share value for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<bool> TransferShareAsync(int shareId, int newMemberId)
    {
        try
        {
            var share = await _context.CooperativeShares.FindAsync(shareId);
            if (share == null)
            {
                return false;
            }

            // Validate new member exists
            var newMemberExists = await _context.Members.AnyAsync(m => m.Id == newMemberId);
            if (!newMemberExists)
            {
                throw new InvalidOperationException($"Member with ID {newMemberId} not found");
            }

            var oldMemberId = share.MemberId;
            share.MemberId = newMemberId;
            share.Status = ShareStatus.Transferred;
            share.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Share {ShareId} transferred from member {OldMemberId} to member {NewMemberId}", 
                shareId, oldMemberId, newMemberId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring share {ShareId} to member {NewMemberId}", shareId, newMemberId);
            throw;
        }
    }
    
    public async Task<IEnumerable<CooperativeShare>> GetActiveSharesAsync()
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .Where(s => s.Status == ShareStatus.Active)
                .OrderBy(s => s.CertificateNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active shares");
            throw;
        }
    }
    
    public async Task<IEnumerable<CooperativeShare>> GetNonActiveSharesAsync()
    {
        try
        {
            return await _context.CooperativeShares
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .Where(s => s.Status != ShareStatus.Active)
                .OrderBy(s => s.CertificateNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving non-active shares");
            throw;
        }
    }
    
    public async Task<decimal> GetOffboardingSharesValueAsync()
    {
        try
        {
            return await _context.CooperativeShares
                .Where(s => s.Status == ShareStatus.Cancelled || s.Status == ShareStatus.Transferred || s.Status == ShareStatus.Suspended)
                .SumAsync(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating offboarding shares value");
            throw;
        }
    }

    public async Task<decimal> GetActiveShareCapitalAsync()
    {
        try
        {
            var activeShares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Where(s => s.Status == ShareStatus.Active)
                .ToListAsync();

            return activeShares
                .Where(s => s.IsFullyPaid)
                .Sum(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating active share capital");
            throw;
        }
    }
    public async Task<decimal> GetUnpaidShareCapitalAsync()
    {
        try
        {
            var activeShares = await _context.CooperativeShares
                .Include(s => s.Payments)
                .Where(s => s.Status == ShareStatus.Active)
                .ToListAsync();

            return activeShares
                .Where(s => !s.IsFullyPaid)
                .Sum(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating unpaid share capital");
            throw;
        }
    }
}