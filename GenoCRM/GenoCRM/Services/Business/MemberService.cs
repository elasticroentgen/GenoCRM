using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using Microsoft.Extensions.Configuration;

namespace GenoCRM.Services.Business;

public interface IMemberService
{
    Task<IEnumerable<Member>> GetAllMembersAsync();
    Task<Member?> GetMemberByIdAsync(int id);
    Task<Member?> GetMemberByNumberAsync(string memberNumber);
    Task<Member> CreateMemberAsync(Member member, int initialShareQuantity = 1);
    Task<Member> UpdateMemberAsync(Member member);
    Task<bool> DeleteMemberAsync(int id);
    Task<bool> MemberNumberExistsAsync(string memberNumber);
    Task<IEnumerable<Member>> SearchMembersAsync(string searchTerm);
    Task<decimal> GetMemberTotalShareValueAsync(int memberId);
    Task<decimal> GetMemberTotalPaymentsAsync(int memberId);
    Task<IEnumerable<Member>> GetMembersByStatusAsync(MemberStatus status);
    Task<string> GenerateNextMemberNumberAsync();
    Task<decimal> GetCurrentShareDenominationAsync();
}

public class MemberService : IMemberService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<MemberService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IShareService _shareService;

    public MemberService(GenoDbContext context, ILogger<MemberService> logger, IConfiguration configuration, IShareService shareService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _shareService = shareService;
    }

    public async Task<IEnumerable<Member>> GetAllMembersAsync()
    {
        try
        {
            return await _context.Members
                .Include(m => m.Shares)
                .Include(m => m.Payments)
                .OrderBy(m => m.MemberNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all members");
            throw;
        }
    }

    public async Task<Member?> GetMemberByIdAsync(int id)
    {
        try
        {
            return await _context.Members
                .Include(m => m.Shares)
                .Include(m => m.Payments)
                .Include(m => m.Dividends)
                .Include(m => m.SubordinatedLoans)
                .Include(m => m.Documents)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<Member?> GetMemberByNumberAsync(string memberNumber)
    {
        try
        {
            return await _context.Members
                .Include(m => m.Shares)
                .Include(m => m.Payments)
                .FirstOrDefaultAsync(m => m.MemberNumber == memberNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member with number {MemberNumber}", memberNumber);
            throw;
        }
    }

    public async Task<Member> CreateMemberAsync(Member member, int initialShareQuantity = 1)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Always auto-generate member number for new members
            member.MemberNumber = await GenerateNextMemberNumberAsync();

            member.CreatedAt = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            // Create initial share(s) for the member
            var shareDenomination = await GetCurrentShareDenominationAsync();
            var initialShare = new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = await _shareService.GenerateNextCertificateNumberAsync(),
                Quantity = initialShareQuantity,
                NominalValue = shareDenomination,
                Value = shareDenomination,
                IssueDate = DateTime.UtcNow,
                Status = ShareStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CooperativeShares.Add(initialShare);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Member created with ID {MemberId}, number {MemberNumber}, and {ShareQuantity} initial shares", 
                member.Id, member.MemberNumber, initialShareQuantity);

            return member;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating member with initial shares");
            throw;
        }
    }

    public async Task<Member> UpdateMemberAsync(Member member)
    {
        try
        {
            var existingMember = await _context.Members.FindAsync(member.Id);
            if (existingMember == null)
            {
                throw new InvalidOperationException($"Member with ID {member.Id} not found");
            }

            // Member number cannot be changed after creation
            // Keep the existing member number
            
            // Update properties (excluding MemberNumber)
            existingMember.FirstName = member.FirstName;
            existingMember.LastName = member.LastName;
            existingMember.Email = member.Email;
            existingMember.Phone = member.Phone;
            existingMember.Street = member.Street;
            existingMember.PostalCode = member.PostalCode;
            existingMember.City = member.City;
            existingMember.Country = member.Country;
            existingMember.BirthDate = member.BirthDate;
            existingMember.Status = member.Status;
            existingMember.LeaveDate = member.LeaveDate;
            existingMember.Notes = member.Notes;
            existingMember.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Member updated with ID {MemberId}", member.Id);

            return existingMember;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member with ID {MemberId}", member.Id);
            throw;
        }
    }

    public async Task<bool> DeleteMemberAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return false;
            }

            // Soft delete - mark as terminated
            member.Status = MemberStatus.Terminated;
            member.LeaveDate = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Member soft deleted with ID {MemberId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<bool> MemberNumberExistsAsync(string memberNumber)
    {
        return await _context.Members.AnyAsync(m => m.MemberNumber == memberNumber);
    }

    public async Task<IEnumerable<Member>> SearchMembersAsync(string searchTerm)
    {
        try
        {
            var lowerSearchTerm = searchTerm.ToLower();
            
            return await _context.Members
                .Include(m => m.Shares)
                .Where(m => m.FirstName.ToLower().Contains(lowerSearchTerm) ||
                           m.LastName.ToLower().Contains(lowerSearchTerm) ||
                           m.MemberNumber.ToLower().Contains(lowerSearchTerm) ||
                           m.Email.ToLower().Contains(lowerSearchTerm))
                .OrderBy(m => m.MemberNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching members with term {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<decimal> GetMemberTotalShareValueAsync(int memberId)
    {
        try
        {
            return await _context.CooperativeShares
                .Where(s => s.MemberId == memberId && s.Status == ShareStatus.Active)
                .SumAsync(s => s.Value * s.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total share value for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<decimal> GetMemberTotalPaymentsAsync(int memberId)
    {
        try
        {
            return await _context.Payments
                .Where(p => p.MemberId == memberId && p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total payments for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<Member>> GetMembersByStatusAsync(MemberStatus status)
    {
        try
        {
            return await _context.Members
                .Include(m => m.Shares)
                .Where(m => m.Status == status)
                .OrderBy(m => m.MemberNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving members by status {Status}", status);
            throw;
        }
    }

    public async Task<string> GenerateNextMemberNumberAsync()
    {
        try
        {
            var lastMember = await _context.Members
                .OrderByDescending(m => m.MemberNumber)
                .FirstOrDefaultAsync();

            if (lastMember == null)
            {
                return "M001";
            }

            // Extract number from member number (assuming format like "M001", "M002", etc.)
            var lastNumber = lastMember.MemberNumber.Substring(1);
            if (int.TryParse(lastNumber, out int number))
            {
                return $"M{(number + 1):D3}";
            }

            // Fallback if parsing fails
            return $"M{DateTime.UtcNow.Ticks % 1000:D3}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next member number");
            throw;
        }
    }

    public Task<decimal> GetCurrentShareDenominationAsync()
    {
        try
        {
            var shareDenomination = _configuration.GetValue<decimal>("CooperativeSettings:ShareDenomination");
            if (shareDenomination <= 0)
            {
                _logger.LogWarning("Share denomination not configured or invalid, using default value of 250");
                return Task.FromResult(250.00m);
            }
            return Task.FromResult(shareDenomination);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share denomination from configuration");
            return Task.FromResult(250.00m); // Default fallback
        }
    }
}