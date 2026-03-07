using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface ILoanPaymentPlanService
{
    Task<LoanPaymentPlan> GeneratePaymentPlanAsync(int subscriptionId);
    Task<LoanPaymentPlan?> GetPaymentPlanBySubscriptionIdAsync(int subscriptionId);
    Task MarkEntryAsPaidAsync(int entryId, DateTime? paidDate = null);
    Task MarkOverdueEntriesAsync();
    Task<List<LoanPaymentPlanEntry>> GetEntriesByMonthAsync(int year, int month);
}

public class LoanPaymentPlanService : ILoanPaymentPlanService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<LoanPaymentPlanService> _logger;

    public LoanPaymentPlanService(GenoDbContext context, ILogger<LoanPaymentPlanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoanPaymentPlan> GeneratePaymentPlanAsync(int subscriptionId)
    {
        try
        {
            var subscription = await _context.LoanSubscriptions
                .Include(s => s.LoanOffer)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId);

            if (subscription == null)
                throw new InvalidOperationException($"Loan subscription with ID {subscriptionId} not found");

            var offer = subscription.LoanOffer;

            // Remove existing plan if any
            var existingPlan = await _context.LoanPaymentPlans
                .Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.LoanSubscriptionId == subscriptionId);
            if (existingPlan != null)
            {
                _context.LoanPaymentPlanEntries.RemoveRange(existingPlan.Entries);
                _context.LoanPaymentPlans.Remove(existingPlan);
                await _context.SaveChangesAsync();
            }

            // Calculate number of periods based on payment interval
            int periodsPerYear = GetPeriodsPerYear(offer.PaymentInterval);
            int totalPeriods = offer.TermMonths * periodsPerYear / 12;
            int monthsPerPeriod = 12 / periodsPerYear;

            // Annuity calculation
            // Rate = P × (r × (1+r)^n) / ((1+r)^n - 1)
            decimal principal = subscription.Amount;
            decimal periodicRate = offer.InterestRate / periodsPerYear;
            decimal annuityRate;

            if (periodicRate == 0)
            {
                annuityRate = principal / totalPeriods;
            }
            else
            {
                double r = (double)periodicRate;
                int n = totalPeriods;
                double factor = Math.Pow(1 + r, n);
                annuityRate = principal * (decimal)(r * factor / (factor - 1));
            }

            annuityRate = Math.Round(annuityRate, 2);

            // Create plan
            var plan = new LoanPaymentPlan
            {
                LoanSubscriptionId = subscriptionId,
                GeneratedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.LoanPaymentPlans.Add(plan);
            await _context.SaveChangesAsync();

            // Generate entries
            decimal remainingBalance = principal;
            var startDate = subscription.SubscriptionDate.AddMonths(offer.GracePeriodMonths);
            var entries = new List<LoanPaymentPlanEntry>();

            for (int i = 1; i <= totalPeriods; i++)
            {
                var dueDate = startDate.AddMonths(i * monthsPerPeriod);
                decimal interest = Math.Round(remainingBalance * periodicRate, 2);
                decimal principalPayment;
                decimal totalAmount;

                if (i == totalPeriods)
                {
                    // Last period: pay remaining balance exactly
                    principalPayment = remainingBalance;
                    totalAmount = principalPayment + interest;
                }
                else
                {
                    totalAmount = annuityRate;
                    principalPayment = totalAmount - interest;
                }

                remainingBalance -= principalPayment;

                // Ensure remaining balance doesn't go negative due to rounding
                if (remainingBalance < 0) remainingBalance = 0;

                entries.Add(new LoanPaymentPlanEntry
                {
                    LoanPaymentPlanId = plan.Id,
                    PeriodNumber = i,
                    DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
                    PrincipalAmount = principalPayment,
                    InterestAmount = interest,
                    TotalAmount = Math.Round(totalAmount, 2),
                    RemainingBalance = Math.Round(remainingBalance, 2),
                    Status = PaymentPlanEntryStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            _context.LoanPaymentPlanEntries.AddRange(entries);
            await _context.SaveChangesAsync();

            plan.Entries = entries;

            _logger.LogInformation("Payment plan generated for subscription {SubscriptionId} with {PeriodCount} periods",
                subscriptionId, totalPeriods);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payment plan for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<LoanPaymentPlan?> GetPaymentPlanBySubscriptionIdAsync(int subscriptionId)
    {
        try
        {
            return await _context.LoanPaymentPlans
                .Include(p => p.Entries.OrderBy(e => e.PeriodNumber))
                .FirstOrDefaultAsync(p => p.LoanSubscriptionId == subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment plan for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task MarkEntryAsPaidAsync(int entryId, DateTime? paidDate = null)
    {
        try
        {
            var entry = await _context.LoanPaymentPlanEntries.FindAsync(entryId);
            if (entry == null)
                throw new InvalidOperationException($"Payment plan entry with ID {entryId} not found");

            entry.Status = PaymentPlanEntryStatus.Paid;
            entry.PaidDate = paidDate ?? DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment plan entry {EntryId} marked as paid", entryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking payment plan entry {EntryId} as paid", entryId);
            throw;
        }
    }

    public async Task MarkOverdueEntriesAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var overdueEntries = await _context.LoanPaymentPlanEntries
                .Where(e => e.Status == PaymentPlanEntryStatus.Pending && e.DueDate < now)
                .ToListAsync();

            foreach (var entry in overdueEntries)
            {
                entry.Status = PaymentPlanEntryStatus.Overdue;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            if (overdueEntries.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} payment plan entries as overdue", overdueEntries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking overdue payment plan entries");
            throw;
        }
    }

    public async Task<List<LoanPaymentPlanEntry>> GetEntriesByMonthAsync(int year, int month)
    {
        try
        {
            return await _context.LoanPaymentPlanEntries
                .Include(e => e.LoanPaymentPlan)
                    .ThenInclude(p => p.LoanSubscription)
                        .ThenInclude(s => s.Member)
                .Include(e => e.LoanPaymentPlan)
                    .ThenInclude(p => p.LoanSubscription)
                        .ThenInclude(s => s.LoanOffer)
                            .ThenInclude(o => o.LoanProject)
                .Where(e => e.DueDate.Year == year && e.DueDate.Month == month
                    && e.LoanPaymentPlan.LoanSubscription.Status == LoanSubscriptionStatus.Active
                    && e.LoanPaymentPlan.LoanSubscription.PaidInDate.HasValue
                    && e.Status != PaymentPlanEntryStatus.Paid)
                .OrderBy(e => e.LoanPaymentPlan.LoanSubscription.Member.LastName)
                    .ThenBy(e => e.LoanPaymentPlan.LoanSubscription.Member.FirstName)
                    .ThenBy(e => e.DueDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment plan entries for {Year}-{Month}", year, month);
            throw;
        }
    }

    private static int GetPeriodsPerYear(PaymentInterval interval)
    {
        return interval switch
        {
            PaymentInterval.Monthly => 12,
            PaymentInterval.Quarterly => 4,
            PaymentInterval.SemiAnnual => 2,
            PaymentInterval.Annual => 1,
            _ => 12
        };
    }
}
