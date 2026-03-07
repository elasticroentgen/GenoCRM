using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface ILoanSubscriptionService
{
    Task<LoanSubscription?> GetSubscriptionByIdAsync(int id);
    Task<IEnumerable<LoanSubscription>> GetSubscriptionsByMemberIdAsync(int memberId);
    Task<IEnumerable<LoanSubscription>> GetSubscriptionsByOfferIdAsync(int offerId);
    Task<LoanSubscription> CreateSubscriptionAsync(LoanSubscription subscription);
    Task<bool> CancelSubscriptionAsync(int id);
    Task<bool> MarkAsPaidInAsync(int id, DateTime? paidInDate = null);
    Task<string> GenerateNextSubscriptionNumberAsync();
}

public class LoanSubscriptionService : ILoanSubscriptionService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<LoanSubscriptionService> _logger;
    private readonly ILoanPaymentPlanService _paymentPlanService;

    public LoanSubscriptionService(
        GenoDbContext context,
        ILogger<LoanSubscriptionService> logger,
        ILoanPaymentPlanService paymentPlanService)
    {
        _context = context;
        _logger = logger;
        _paymentPlanService = paymentPlanService;
    }

    public async Task<LoanSubscription?> GetSubscriptionByIdAsync(int id)
    {
        try
        {
            return await _context.LoanSubscriptions
                .Include(s => s.LoanOffer)
                    .ThenInclude(o => o.LoanProject)
                .Include(s => s.Member)
                .Include(s => s.PaymentPlan)
                    .ThenInclude(p => p!.Entries.OrderBy(e => e.PeriodNumber))
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan subscription with ID {SubscriptionId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<LoanSubscription>> GetSubscriptionsByMemberIdAsync(int memberId)
    {
        try
        {
            return await _context.LoanSubscriptions
                .Include(s => s.LoanOffer)
                    .ThenInclude(o => o.LoanProject)
                .Include(s => s.PaymentPlan)
                    .ThenInclude(p => p!.Entries)
                .Where(s => s.MemberId == memberId)
                .OrderByDescending(s => s.SubscriptionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscriptions for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<LoanSubscription>> GetSubscriptionsByOfferIdAsync(int offerId)
    {
        try
        {
            return await _context.LoanSubscriptions
                .Include(s => s.Member)
                .Include(s => s.PaymentPlan)
                    .ThenInclude(p => p!.Entries)
                .Where(s => s.LoanOfferId == offerId)
                .OrderByDescending(s => s.SubscriptionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscriptions for offer {OfferId}", offerId);
            throw;
        }
    }

    public async Task<LoanSubscription> CreateSubscriptionAsync(LoanSubscription subscription)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validate offer exists and is open
            var offer = await _context.LoanOffers
                .Include(o => o.LoanProject)
                .FirstOrDefaultAsync(o => o.Id == subscription.LoanOfferId);
            if (offer == null)
                throw new InvalidOperationException($"Loan offer with ID {subscription.LoanOfferId} not found");
            if (offer.Status != LoanOfferStatus.Open)
                throw new InvalidOperationException("Loan offer is not open for subscriptions");

            // Validate member exists
            var memberExists = await _context.Members.AnyAsync(m => m.Id == subscription.MemberId);
            if (!memberExists)
                throw new InvalidOperationException($"Member with ID {subscription.MemberId} not found");

            // Validate amount constraints
            if (offer.MinSubscriptionAmount.HasValue && subscription.Amount < offer.MinSubscriptionAmount.Value)
                throw new InvalidOperationException($"Subscription amount must be at least {offer.MinSubscriptionAmount.Value:F2}");
            if (offer.MaxSubscriptionAmount.HasValue && subscription.Amount > offer.MaxSubscriptionAmount.Value)
                throw new InvalidOperationException($"Subscription amount must not exceed {offer.MaxSubscriptionAmount.Value:F2}");

            // Generate subscription number
            if (string.IsNullOrEmpty(subscription.SubscriptionNumber))
                subscription.SubscriptionNumber = await GenerateNextSubscriptionNumberAsync();

            subscription.CreatedAt = DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;

            _context.LoanSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Automatically generate payment plan
            await _paymentPlanService.GeneratePaymentPlanAsync(subscription.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Loan subscription created with ID {SubscriptionId}, number {SubscriptionNumber}",
                subscription.Id, subscription.SubscriptionNumber);

            return subscription;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating loan subscription");
            throw;
        }
    }

    public async Task<bool> CancelSubscriptionAsync(int id)
    {
        try
        {
            var subscription = await _context.LoanSubscriptions
                .Include(s => s.PaymentPlan)
                    .ThenInclude(p => p!.Entries)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (subscription == null) return false;

            if (subscription.PaidInDate.HasValue)
                throw new InvalidOperationException("Cannot cancel a subscription that has already been paid in");

            // Remove payment plan and entries
            if (subscription.PaymentPlan != null)
            {
                _context.LoanPaymentPlanEntries.RemoveRange(subscription.PaymentPlan.Entries);
                _context.LoanPaymentPlans.Remove(subscription.PaymentPlan);
            }

            subscription.Status = LoanSubscriptionStatus.Cancelled;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan subscription cancelled with ID {SubscriptionId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling loan subscription with ID {SubscriptionId}", id);
            throw;
        }
    }

    public async Task<bool> MarkAsPaidInAsync(int id, DateTime? paidInDate = null)
    {
        try
        {
            var subscription = await _context.LoanSubscriptions.FindAsync(id);
            if (subscription == null) return false;

            subscription.PaidInDate = paidInDate ?? DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan subscription {SubscriptionId} marked as paid in", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking loan subscription {SubscriptionId} as paid in", id);
            throw;
        }
    }

    public async Task<string> GenerateNextSubscriptionNumberAsync()
    {
        try
        {
            var existingNumbers = await _context.LoanSubscriptions
                .IgnoreQueryFilters()
                .Select(s => s.SubscriptionNumber)
                .ToListAsync();

            if (!existingNumbers.Any())
                return "LS0001";

            var highestNumber = existingNumbers
                .Select(n =>
                {
                    if (n.StartsWith("LS") && int.TryParse(n.Substring(2), out int num))
                        return num;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            var nextNumber = highestNumber + 1;
            var candidate = $"LS{nextNumber:D4}";

            while (await _context.LoanSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.SubscriptionNumber == candidate))
            {
                nextNumber++;
                candidate = $"LS{nextNumber:D4}";
            }

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next subscription number");
            throw;
        }
    }
}
