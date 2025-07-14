using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IDividendService
{
    Task<IEnumerable<Dividend>> GetAllDividendsAsync();
    Task<Dividend?> GetDividendByIdAsync(int id);
    Task<IEnumerable<Dividend>> GetDividendsByMemberIdAsync(int memberId);
    Task<IEnumerable<Dividend>> GetDividendsByFiscalYearAsync(int fiscalYear);
    Task<Dividend> CreateDividendAsync(Dividend dividend);
    Task<Dividend> UpdateDividendAsync(Dividend dividend);
    Task<bool> DeleteDividendAsync(int id);
    Task<decimal> CalculateDividendForMemberAsync(int memberId, int fiscalYear, decimal rate);
    Task<IEnumerable<Dividend>> CalculateDividendsForAllMembersAsync(int fiscalYear, decimal rate);
    Task<decimal> GetTotalDividendsByYearAsync(int fiscalYear);
    Task<bool> ApproveDividendAsync(int dividendId);
    Task<bool> PayDividendAsync(int dividendId);
}

public class DividendService : IDividendService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<DividendService> _logger;

    public DividendService(GenoDbContext context, ILogger<DividendService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Dividend>> GetAllDividendsAsync()
    {
        try
        {
            return await _context.Dividends
                .Include(d => d.Member)
                .Include(d => d.Share)
                .OrderByDescending(d => d.FiscalYear)
                .ThenBy(d => d.Member.MemberNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all dividends");
            throw;
        }
    }

    public async Task<Dividend?> GetDividendByIdAsync(int id)
    {
        try
        {
            return await _context.Dividends
                .Include(d => d.Member)
                .Include(d => d.Share)
                .FirstOrDefaultAsync(d => d.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dividend with ID {DividendId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Dividend>> GetDividendsByMemberIdAsync(int memberId)
    {
        try
        {
            return await _context.Dividends
                .Include(d => d.Share)
                .Where(d => d.MemberId == memberId)
                .OrderByDescending(d => d.FiscalYear)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dividends for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<Dividend>> GetDividendsByFiscalYearAsync(int fiscalYear)
    {
        try
        {
            return await _context.Dividends
                .Include(d => d.Member)
                .Include(d => d.Share)
                .Where(d => d.FiscalYear == fiscalYear)
                .OrderBy(d => d.Member.MemberNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dividends for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<Dividend> CreateDividendAsync(Dividend dividend)
    {
        try
        {
            // Validate member exists
            var memberExists = await _context.Members.AnyAsync(m => m.Id == dividend.MemberId);
            if (!memberExists)
            {
                throw new InvalidOperationException($"Member with ID {dividend.MemberId} not found");
            }

            // Validate share exists if provided
            if (dividend.ShareId.HasValue)
            {
                var shareExists = await _context.CooperativeShares.AnyAsync(s => s.Id == dividend.ShareId.Value);
                if (!shareExists)
                {
                    throw new InvalidOperationException($"Share with ID {dividend.ShareId.Value} not found");
                }
            }

            // Check if dividend already exists for this member and fiscal year
            var existingDividend = await _context.Dividends
                .FirstOrDefaultAsync(d => d.MemberId == dividend.MemberId && 
                                        d.FiscalYear == dividend.FiscalYear &&
                                        d.ShareId == dividend.ShareId);

            if (existingDividend != null)
            {
                throw new InvalidOperationException($"Dividend already exists for member {dividend.MemberId} and fiscal year {dividend.FiscalYear}");
            }

            dividend.CreatedAt = DateTime.UtcNow;
            dividend.UpdatedAt = DateTime.UtcNow;

            _context.Dividends.Add(dividend);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Dividend created with ID {DividendId} for member {MemberId} fiscal year {FiscalYear}", 
                dividend.Id, dividend.MemberId, dividend.FiscalYear);

            return dividend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dividend");
            throw;
        }
    }

    public async Task<Dividend> UpdateDividendAsync(Dividend dividend)
    {
        try
        {
            var existingDividend = await _context.Dividends.FindAsync(dividend.Id);
            if (existingDividend == null)
            {
                throw new InvalidOperationException($"Dividend with ID {dividend.Id} not found");
            }

            // Update properties
            existingDividend.Amount = dividend.Amount;
            existingDividend.Rate = dividend.Rate;
            existingDividend.BaseAmount = dividend.BaseAmount;
            existingDividend.DeclarationDate = dividend.DeclarationDate;
            existingDividend.PaymentDate = dividend.PaymentDate;
            existingDividend.Status = dividend.Status;
            existingDividend.TaxWithheld = dividend.TaxWithheld;
            existingDividend.NetAmount = dividend.NetAmount;
            existingDividend.Notes = dividend.Notes;
            existingDividend.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Dividend updated with ID {DividendId}", dividend.Id);

            return existingDividend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dividend with ID {DividendId}", dividend.Id);
            throw;
        }
    }

    public async Task<bool> DeleteDividendAsync(int id)
    {
        try
        {
            var dividend = await _context.Dividends.FindAsync(id);
            if (dividend == null)
            {
                return false;
            }

            // Only allow deletion if not yet paid
            if (dividend.Status == DividendStatus.Paid)
            {
                throw new InvalidOperationException("Cannot delete a paid dividend");
            }

            _context.Dividends.Remove(dividend);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Dividend deleted with ID {DividendId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dividend with ID {DividendId}", id);
            throw;
        }
    }

    public async Task<decimal> CalculateDividendForMemberAsync(int memberId, int fiscalYear, decimal rate)
    {
        try
        {
            // Get member's active shares for the fiscal year
            var memberShares = await _context.CooperativeShares
                .Where(s => s.MemberId == memberId && 
                           s.Status == ShareStatus.Active &&
                           s.IssueDate.Year <= fiscalYear)
                .ToListAsync();

            if (!memberShares.Any())
            {
                return 0;
            }

            // Calculate total share value as base for dividend
            decimal totalShareValue = memberShares.Sum(s => s.Value * s.Quantity);

            // Calculate dividend amount
            decimal dividendAmount = totalShareValue * rate;

            return dividendAmount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating dividend for member {MemberId} fiscal year {FiscalYear}", memberId, fiscalYear);
            throw;
        }
    }

    public async Task<IEnumerable<Dividend>> CalculateDividendsForAllMembersAsync(int fiscalYear, decimal rate)
    {
        try
        {
            var dividends = new List<Dividend>();

            // Get all active members
            var activeMembers = await _context.Members
                .Where(m => m.Status == MemberStatus.Active)
                .Include(m => m.Shares.Where(s => s.Status == ShareStatus.Active && s.IssueDate.Year <= fiscalYear))
                .ToListAsync();

            foreach (var member in activeMembers)
            {
                if (member.Shares.Any())
                {
                    decimal totalShareValue = member.Shares.Sum(s => s.Value * s.Quantity);
                    decimal dividendAmount = totalShareValue * rate;

                    if (dividendAmount > 0)
                    {
                        var dividend = new Dividend
                        {
                            MemberId = member.Id,
                            FiscalYear = fiscalYear,
                            Amount = dividendAmount,
                            Rate = rate,
                            BaseAmount = totalShareValue,
                            DeclarationDate = DateTime.UtcNow,
                            Status = DividendStatus.Declared,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        dividends.Add(dividend);
                    }
                }
            }

            // Save all dividends
            if (dividends.Any())
            {
                _context.Dividends.AddRange(dividends);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Calculated and created {Count} dividends for fiscal year {FiscalYear}", 
                    dividends.Count, fiscalYear);
            }

            return dividends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating dividends for all members fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<decimal> GetTotalDividendsByYearAsync(int fiscalYear)
    {
        try
        {
            return await _context.Dividends
                .Where(d => d.FiscalYear == fiscalYear)
                .SumAsync(d => d.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total dividends for fiscal year {FiscalYear}", fiscalYear);
            throw;
        }
    }

    public async Task<bool> ApproveDividendAsync(int dividendId)
    {
        try
        {
            var dividend = await _context.Dividends.FindAsync(dividendId);
            if (dividend == null)
            {
                return false;
            }

            if (dividend.Status != DividendStatus.Declared)
            {
                throw new InvalidOperationException($"Dividend must be in Declared status to approve, current status: {dividend.Status}");
            }

            dividend.Status = DividendStatus.Approved;
            dividend.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Dividend approved with ID {DividendId}", dividendId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving dividend with ID {DividendId}", dividendId);
            throw;
        }
    }

    public async Task<bool> PayDividendAsync(int dividendId)
    {
        try
        {
            var dividend = await _context.Dividends.FindAsync(dividendId);
            if (dividend == null)
            {
                return false;
            }

            if (dividend.Status != DividendStatus.Approved)
            {
                throw new InvalidOperationException($"Dividend must be approved before payment, current status: {dividend.Status}");
            }

            dividend.Status = DividendStatus.Paid;
            dividend.PaymentDate = DateTime.UtcNow;
            dividend.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Dividend paid with ID {DividendId}", dividendId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying dividend with ID {DividendId}", dividendId);
            throw;
        }
    }
}