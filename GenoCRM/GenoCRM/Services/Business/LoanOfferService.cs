using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface ILoanOfferService
{
    Task<LoanOffer?> GetOfferByIdAsync(int id);
    Task<IEnumerable<LoanOffer>> GetOffersByProjectIdAsync(int projectId);
    Task<LoanOffer> CreateOfferAsync(LoanOffer offer);
    Task<LoanOffer> UpdateOfferAsync(LoanOffer offer);
    Task<bool> DeleteOfferAsync(int id);
}

public class LoanOfferService : ILoanOfferService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<LoanOfferService> _logger;

    public LoanOfferService(GenoDbContext context, ILogger<LoanOfferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoanOffer?> GetOfferByIdAsync(int id)
    {
        try
        {
            return await _context.LoanOffers
                .Include(o => o.LoanProject)
                .Include(o => o.Subscriptions)
                    .ThenInclude(s => s.Member)
                .Include(o => o.Subscriptions)
                    .ThenInclude(s => s.PaymentPlan)
                        .ThenInclude(p => p!.Entries)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan offer with ID {OfferId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<LoanOffer>> GetOffersByProjectIdAsync(int projectId)
    {
        try
        {
            return await _context.LoanOffers
                .Include(o => o.Subscriptions)
                .Where(o => o.LoanProjectId == projectId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan offers for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<LoanOffer> CreateOfferAsync(LoanOffer offer)
    {
        try
        {
            var projectExists = await _context.LoanProjects.AnyAsync(p => p.Id == offer.LoanProjectId);
            if (!projectExists)
                throw new InvalidOperationException($"Loan project with ID {offer.LoanProjectId} not found");

            offer.CreatedAt = DateTime.UtcNow;
            offer.UpdatedAt = DateTime.UtcNow;

            _context.LoanOffers.Add(offer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan offer created with ID {OfferId} for project {ProjectId}",
                offer.Id, offer.LoanProjectId);

            return offer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loan offer");
            throw;
        }
    }

    public async Task<LoanOffer> UpdateOfferAsync(LoanOffer offer)
    {
        try
        {
            var existing = await _context.LoanOffers.FindAsync(offer.Id);
            if (existing == null)
                throw new InvalidOperationException($"Loan offer with ID {offer.Id} not found");

            existing.Title = offer.Title;
            existing.InterestRate = offer.InterestRate;
            existing.TermMonths = offer.TermMonths;
            existing.PaymentInterval = offer.PaymentInterval;
            existing.RepaymentType = offer.RepaymentType;
            existing.MinSubscriptionAmount = offer.MinSubscriptionAmount;
            existing.MaxSubscriptionAmount = offer.MaxSubscriptionAmount;
            existing.Status = offer.Status;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan offer updated with ID {OfferId}", offer.Id);

            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan offer with ID {OfferId}", offer.Id);
            throw;
        }
    }

    public async Task<bool> DeleteOfferAsync(int id)
    {
        try
        {
            var offer = await _context.LoanOffers.FindAsync(id);
            if (offer == null) return false;

            offer.Status = LoanOfferStatus.Cancelled;
            offer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan offer cancelled with ID {OfferId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting loan offer with ID {OfferId}", id);
            throw;
        }
    }
}
