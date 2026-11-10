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
    Task<List<LoanPaymentPlanEntry>> GetEntriesByYearAsync(int year);
    Task<(int MinYear, int MaxYear)?> GetPaymentYearRangeAsync();
    Task<int> RegeneratePaymentPlansAsync();
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

            decimal principal = subscription.Amount;
            decimal periodicRate = offer.InterestRate / periodsPerYear;

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
            // Interest accrues from the date funds were actually received (PaidInDate). The fallback to
            // SubscriptionDate is defensive — the new flow only triggers GeneratePaymentPlanAsync after
            // pay-in is confirmed, so PaidInDate should always be set when this runs.
            decimal remainingBalance = principal;
            var basisDate = subscription.PaidInDate ?? subscription.SubscriptionDate;
            var startDate = basisDate.Date.AddMonths(offer.GracePeriodMonths);
            var entries = new List<LoanPaymentPlanEntry>();

            if (offer.RepaymentType == RepaymentType.BulletLoan && offer.PaymentInterval == PaymentInterval.Annual)
            {
                // Annual bullet loan: interest paid out every 31.12. (pro rata act/365 for partial years),
                // principal + remaining interest paid together at maturity.
                entries.AddRange(CalculateAnnualBulletEntries(principal, offer.InterestRate, startDate, offer.TermMonths));
                foreach (var e in entries) e.LoanPaymentPlanId = plan.Id;
                totalPeriods = entries.Count;
            }
            else if (offer.RepaymentType == RepaymentType.BulletLoan)
            {
                // Bullet loan: interest-only payments, full principal repaid at end
                for (int i = 1; i <= totalPeriods; i++)
                {
                    var dueDate = startDate.AddMonths(i * monthsPerPeriod);
                    decimal interest = Math.Round(principal * periodicRate, 2);
                    decimal principalPayment = 0;

                    if (i == totalPeriods)
                    {
                        // Last period: repay full principal + interest
                        principalPayment = principal;
                        remainingBalance = 0;
                    }

                    entries.Add(new LoanPaymentPlanEntry
                    {
                        LoanPaymentPlanId = plan.Id,
                        PeriodNumber = i,
                        DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
                        PrincipalAmount = principalPayment,
                        InterestAmount = interest,
                        TotalAmount = Math.Round(principalPayment + interest, 2),
                        RemainingBalance = i == totalPeriods ? 0 : principal,
                        Status = PaymentPlanEntryStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Annuity calculation
                // Rate = P × (r × (1+r)^n) / ((1+r)^n - 1)
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

    public async Task<List<LoanPaymentPlanEntry>> GetEntriesByYearAsync(int year)
    {
        try
        {
            var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddYears(1);
            return await _context.LoanPaymentPlanEntries
                .Include(e => e.LoanPaymentPlan)
                    .ThenInclude(p => p.LoanSubscription)
                        .ThenInclude(s => s.Member)
                .Include(e => e.LoanPaymentPlan)
                    .ThenInclude(p => p.LoanSubscription)
                        .ThenInclude(s => s.LoanOffer)
                            .ThenInclude(o => o.LoanProject)
                .Where(e => e.DueDate >= from && e.DueDate < to
                    && e.LoanPaymentPlan.LoanSubscription.Status != LoanSubscriptionStatus.Cancelled
                    && e.LoanPaymentPlan.LoanSubscription.PaidInDate.HasValue)
                .OrderBy(e => e.DueDate)
                    .ThenBy(e => e.LoanPaymentPlan.LoanSubscription.Member.LastName)
                    .ThenBy(e => e.LoanPaymentPlan.LoanSubscription.Member.FirstName)
                    .ThenBy(e => e.LoanPaymentPlan.LoanSubscription.SubscriptionNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment plan entries for {Year}", year);
            throw;
        }
    }

    public async Task<(int MinYear, int MaxYear)?> GetPaymentYearRangeAsync()
    {
        var query = _context.LoanPaymentPlanEntries
            .Where(e => e.LoanPaymentPlan.LoanSubscription.Status != LoanSubscriptionStatus.Cancelled
                && e.LoanPaymentPlan.LoanSubscription.PaidInDate.HasValue);
        if (!await query.AnyAsync()) return null;
        var min = await query.MinAsync(e => e.DueDate);
        var max = await query.MaxAsync(e => e.DueDate);
        return (min.Year, max.Year);
    }

    public async Task<int> RegeneratePaymentPlansAsync()
    {
        try
        {
            var subscriptionIds = await _context.LoanSubscriptions
                .Where(s => s.Status == LoanSubscriptionStatus.Active
                    && s.PaidInDate.HasValue
                    && (s.PaymentPlan == null || !s.PaymentPlan.Entries.Any(e => e.Status == PaymentPlanEntryStatus.Paid)))
                .Select(s => s.Id)
                .ToListAsync();

            foreach (var id in subscriptionIds)
            {
                await GeneratePaymentPlanAsync(id);
            }

            _logger.LogInformation("Regenerated {Count} payment plans", subscriptionIds.Count);
            return subscriptionIds.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating payment plans");
            throw;
        }
    }

    /// <summary>
    /// Annual bullet loan schedule: interest is paid out each 31.12. — pro rata (act/365) for partial
    /// calendar years, the full annual rate for complete calendar years. Principal is repaid at
    /// <paramref name="start"/> + <paramref name="termMonths"/>, together with the interest accrued
    /// since the last 31.12. in a single entry.
    /// </summary>
    public static List<LoanPaymentPlanEntry> CalculateAnnualBulletEntries(
        decimal principal, decimal annualRate, DateTime start, int termMonths)
    {
        start = start.Date;
        var maturity = start.AddMonths(termMonths);
        var entries = new List<LoanPaymentPlanEntry>();
        var period = 0;
        var now = DateTime.UtcNow;

        for (var year = start.Year; year <= maturity.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = yearStart.AddYears(1);
            var from = start > yearStart ? start : yearStart;
            var to = maturity < yearEnd ? maturity : yearEnd;
            var isFinal = year == maturity.Year;

            var days = Math.Max(0, (to - from).Days);
            var isFullYear = from == yearStart && to == yearEnd;
            var interest = isFullYear
                ? Math.Round(principal * annualRate, 2)
                : Math.Round(principal * annualRate * days / 365m, 2);
            var principalPayment = isFinal ? principal : 0m;

            if (interest == 0 && principalPayment == 0) continue;

            var dueDate = isFinal ? maturity : new DateTime(year, 12, 31);
            entries.Add(new LoanPaymentPlanEntry
            {
                PeriodNumber = ++period,
                DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
                InterestPeriodStart = days > 0 ? DateTime.SpecifyKind(from, DateTimeKind.Utc) : null,
                InterestPeriodEnd = days > 0 ? DateTime.SpecifyKind(to, DateTimeKind.Utc) : null,
                InterestDays = days > 0 ? days : null,
                PrincipalAmount = principalPayment,
                InterestAmount = interest,
                TotalAmount = principalPayment + interest,
                RemainingBalance = isFinal ? 0 : principal,
                Status = PaymentPlanEntryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return entries;
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
