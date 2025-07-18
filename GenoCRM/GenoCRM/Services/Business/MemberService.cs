using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

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
    Task<int> GetMaxSharesPerMemberAsync();
    Task<bool> OffboardMemberAsync(int id);
    Task<bool> CanOffboardMemberAsync(int id);
    Task<IEnumerable<Member>> GetMembersReadyForDeletionAsync();
    Task<bool> FinallyDeleteMemberAsync(int id);
    Task<int> ProcessOffboardingMembersAsync();
    Task<bool> SubmitTerminationNoticeAsync(int id);
    Task<bool> CanSubmitTerminationNoticeAsync(int id);
    Task<DateTime?> GetEarliestTerminationDateAsync(int id);
    Task<IEnumerable<Member>> GetMembersReadyForOffboardingAsync();
}

public class MemberService : IMemberService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<MemberService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IShareService _shareService;
    private readonly IFiscalYearService _fiscalYearService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MemberService(GenoDbContext context, ILogger<MemberService> logger, IConfiguration configuration, IShareService shareService, IFiscalYearService fiscalYearService, IAuditService auditService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _shareService = shareService;
        _fiscalYearService = fiscalYearService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
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
            // Validate initial share quantity against max shares per member
            var maxSharesPerMember = await GetMaxSharesPerMemberAsync();
            if (initialShareQuantity > maxSharesPerMember)
            {
                throw new InvalidOperationException($"Initial share quantity ({initialShareQuantity}) exceeds maximum allowed shares per member ({maxSharesPerMember})");
            }

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

            // Log audit trail
            await AuditHelper.LogAuditAsync(
                _auditService,
                _httpContextAccessor,
                AuditAction.Create,
                nameof(Member),
                member.Id.ToString(),
                AuditHelper.GetMemberDescription(member),
                Permissions.CreateMembers,
                new { member.MemberNumber, member.FullName, initialShareQuantity });

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

            // Log audit trail
            await AuditHelper.LogAuditAsync(
                _auditService,
                _httpContextAccessor,
                AuditAction.Update,
                nameof(Member),
                member.Id.ToString(),
                AuditHelper.GetMemberDescription(existingMember),
                Permissions.EditMembers,
                new { 
                    Changes = new {
                        FirstName = member.FirstName,
                        LastName = member.LastName,
                        Email = member.Email,
                        Phone = member.Phone,
                        Status = member.Status
                    }
                });

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
            // Get all existing member numbers including terminated members (ignore query filters)
            var existingNumbers = await _context.Members
                .IgnoreQueryFilters()
                .Select(m => m.MemberNumber)
                .ToListAsync();

            if (!existingNumbers.Any())
            {
                return "M001";
            }

            // Extract numeric parts and find the highest number
            var highestNumber = existingNumbers
                .Select(mn => {
                    if (mn.StartsWith("M") && int.TryParse(mn.Substring(1), out int num))
                        return num;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            // Generate next number
            var nextNumber = highestNumber + 1;
            var candidateNumber = $"M{nextNumber:D3}";

            // Double-check that the number doesn't exist across all members (including terminated)
            while (await _context.Members.IgnoreQueryFilters().AnyAsync(m => m.MemberNumber == candidateNumber))
            {
                nextNumber++;
                candidateNumber = $"M{nextNumber:D3}";
            }

            return candidateNumber;
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

    public Task<int> GetMaxSharesPerMemberAsync()
    {
        try
        {
            var maxShares = _configuration.GetValue<int>("CooperativeSettings:MaxSharesPerMember");
            if (maxShares <= 0)
            {
                _logger.LogWarning("Max shares per member not configured or invalid, using default value of 100");
                return Task.FromResult(100);
            }
            return Task.FromResult(maxShares);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting max shares per member from configuration");
            return Task.FromResult(100); // Default fallback
        }
    }

    public async Task<bool> SubmitTerminationNoticeAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return false;
            }

            if (!await CanSubmitTerminationNoticeAsync(id))
            {
                return false;
            }

            // Submit termination notice
            member.TerminationNoticeDate = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Termination notice submitted for member with ID {MemberId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting termination notice for member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<bool> CanSubmitTerminationNoticeAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return false;
            }

            // Can only submit termination notice for active members
            if (member.Status != MemberStatus.Active)
            {
                return false;
            }

            // Cannot submit if already submitted
            if (member.TerminationNoticeDate.HasValue)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if termination notice can be submitted for member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<DateTime?> GetEarliestTerminationDateAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null || !member.TerminationNoticeDate.HasValue)
            {
                return null;
            }

            // Calculate 2 years from notice date, ending at fiscal year end
            var noticeDate = member.TerminationNoticeDate.Value;
            var twoYearsLater = noticeDate.AddYears(2);
            
            // Find the fiscal year end that occurs on or after the 2-year mark
            var fiscalYear = _fiscalYearService.GetFiscalYearForDate(twoYearsLater);
            var fiscalYearEnd = _fiscalYearService.GetFiscalYearEnd(fiscalYear);
            
            // If the 2-year date is after the fiscal year end, move to next fiscal year
            if (twoYearsLater > fiscalYearEnd)
            {
                fiscalYearEnd = _fiscalYearService.GetFiscalYearEnd(fiscalYear + 1);
            }

            return fiscalYearEnd;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating earliest termination date for member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Member>> GetMembersReadyForOffboardingAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var members = await _context.Members
                .Where(m => m.TerminationNoticeDate.HasValue && m.Status == MemberStatus.Active)
                .ToListAsync();

            var readyMembers = new List<Member>();

            foreach (var member in members)
            {
                var earliestTerminationDate = await GetEarliestTerminationDateAsync(member.Id);
                if (earliestTerminationDate.HasValue && now >= earliestTerminationDate.Value)
                {
                    readyMembers.Add(member);
                }
            }

            return readyMembers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members ready for offboarding");
            throw;
        }
    }

    public async Task<bool> OffboardMemberAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return false;
            }

            if (!await CanOffboardMemberAsync(id))
            {
                return false;
            }

            // Mark member as offboarding
            member.Status = MemberStatus.Offboarding;
            member.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Member marked for offboarding with ID {MemberId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error offboarding member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<bool> CanOffboardMemberAsync(int id)
    {
        try
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null)
            {
                return false;
            }

            // Can only offboard active members
            if (member.Status != MemberStatus.Active)
            {
                return false;
            }

            // Must have submitted termination notice
            if (!member.TerminationNoticeDate.HasValue)
            {
                return false;
            }

            // Check if 2-year notice period has passed
            var earliestTerminationDate = await GetEarliestTerminationDateAsync(id);
            if (!earliestTerminationDate.HasValue || DateTime.UtcNow < earliestTerminationDate.Value)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if member can be offboarded with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Member>> GetMembersReadyForDeletionAsync()
    {
        try
        {
            var canProcessOffboarding = _fiscalYearService.CanProcessOffboarding();
            if (!canProcessOffboarding)
            {
                return Enumerable.Empty<Member>();
            }

            // Get members who have been in offboarding status and fiscal year has ended
            var members = await _context.Members
                .Where(m => m.Status == MemberStatus.Offboarding)
                .Include(m => m.Shares)
                .Include(m => m.Payments)
                .Include(m => m.Dividends)
                .ToListAsync();

            return members;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members ready for deletion");
            throw;
        }
    }

    public async Task<bool> FinallyDeleteMemberAsync(int id)
    {
        try
        {
            var member = await _context.Members
                .Include(m => m.Shares)
                .Include(m => m.Payments)
                .Include(m => m.Dividends)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (member == null)
            {
                return false;
            }

            // Can only delete members in offboarding status after fiscal year end
            if (member.Status != MemberStatus.Offboarding || !_fiscalYearService.CanProcessOffboarding())
            {
                return false;
            }

            // Check if all shares have been returned (cancelled)
            var activeShares = member.Shares.Where(s => s.Status == ShareStatus.Active).ToList();
            if (activeShares.Any())
            {
                _logger.LogWarning("Cannot delete member {MemberId} - still has active shares", id);
                return false;
            }

            // Check if all dividends have been paid
            var unpaidDividends = member.Dividends.Where(d => d.Status != DividendStatus.Paid).ToList();
            if (unpaidDividends.Any())
            {
                _logger.LogWarning("Cannot delete member {MemberId} - still has unpaid dividends", id);
                return false;
            }

            // Mark as terminated instead of hard delete to preserve audit trail
            member.Status = MemberStatus.Terminated;
            member.LeaveDate = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Member finally processed for deletion with ID {MemberId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finally deleting member with ID {MemberId}", id);
            throw;
        }
    }

    public async Task<int> ProcessOffboardingMembersAsync()
    {
        try
        {
            var membersToProcess = await GetMembersReadyForDeletionAsync();
            var processedCount = 0;

            foreach (var member in membersToProcess)
            {
                if (await FinallyDeleteMemberAsync(member.Id))
                {
                    processedCount++;
                }
            }

            _logger.LogInformation("Processed {ProcessedCount} members for offboarding completion", processedCount);

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing offboarding members");
            throw;
        }
    }
}