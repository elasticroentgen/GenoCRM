using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class LoanPaymentPlanServiceTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly LoanPaymentPlanService _service;

    public LoanPaymentPlanServiceTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _service = new LoanPaymentPlanService(_context, new Mock<ILogger<LoanPaymentPlanService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void AnnualBullet_PartialFirstAndLastYear_ProRataAct365AndCombinedFinalPayment()
    {
        var entries = LoanPaymentPlanService.CalculateAnnualBulletEntries(5000m, 0.04m, new DateTime(2026, 4, 10), 24);

        Assert.Equal(3, entries.Count);

        Assert.Equal(new DateTime(2026, 12, 31), entries[0].DueDate);
        Assert.Equal(266, entries[0].InterestDays);
        Assert.Equal(145.75m, entries[0].InterestAmount);
        Assert.Equal(0m, entries[0].PrincipalAmount);
        Assert.Equal(new DateTime(2026, 4, 10), entries[0].InterestPeriodStart);
        Assert.Equal(new DateTime(2027, 1, 1), entries[0].InterestPeriodEnd);

        Assert.Equal(new DateTime(2027, 12, 31), entries[1].DueDate);
        Assert.Equal(200.00m, entries[1].InterestAmount);
        Assert.Equal(5000m, entries[1].RemainingBalance);

        var final = entries[2];
        Assert.Equal(new DateTime(2028, 4, 10), final.DueDate);
        Assert.Equal(100, final.InterestDays);
        Assert.Equal(54.79m, final.InterestAmount);
        Assert.Equal(5000m, final.PrincipalAmount);
        Assert.Equal(5054.79m, final.TotalAmount);
        Assert.Equal(0m, final.RemainingBalance);

        Assert.Equal(new[] { 1, 2, 3 }, entries.Select(e => e.PeriodNumber));
    }

    [Fact]
    public void AnnualBullet_FullLeapYear_GetsFullAnnualRate()
    {
        var entries = LoanPaymentPlanService.CalculateAnnualBulletEntries(10000m, 0.045m, new DateTime(2027, 6, 1), 24);

        var leapYear = entries.Single(e => e.DueDate == new DateTime(2028, 12, 31));
        Assert.Equal(366, leapYear.InterestDays);
        Assert.Equal(450.00m, leapYear.InterestAmount);
    }

    [Fact]
    public void AnnualBullet_PaidInOnJanuaryFirst_OnlyFullYearsAndPrincipalOnlyFinalEntry()
    {
        var entries = LoanPaymentPlanService.CalculateAnnualBulletEntries(1000m, 0.04m, new DateTime(2026, 1, 1), 24);

        Assert.Equal(3, entries.Count);
        Assert.All(entries.Take(2), e => Assert.Equal(40.00m, e.InterestAmount));
        var final = entries[2];
        Assert.Equal(new DateTime(2028, 1, 1), final.DueDate);
        Assert.Equal(0m, final.InterestAmount);
        Assert.Equal(1000m, final.PrincipalAmount);
        Assert.Null(final.InterestPeriodStart);
    }

    [Fact]
    public void AnnualBullet_PaidInOnDecember31_OneDayOfInterestInFirstYear()
    {
        var entries = LoanPaymentPlanService.CalculateAnnualBulletEntries(36500m, 0.04m, new DateTime(2026, 12, 31), 12);

        Assert.Equal(1, entries[0].InterestDays);
        Assert.Equal(4.00m, entries[0].InterestAmount);
        Assert.Equal(new DateTime(2027, 12, 31), entries[1].DueDate);
        Assert.Equal(36500m, entries[1].PrincipalAmount);
        Assert.Equal(364, entries[1].InterestDays);
    }

    [Fact]
    public void AnnualBullet_TotalInterestCoversExactDays()
    {
        var entries = LoanPaymentPlanService.CalculateAnnualBulletEntries(5000m, 0.0425m, new DateTime(2026, 5, 15), 60);

        var start = new DateTime(2026, 5, 15);
        Assert.Equal((start.AddMonths(60) - start).Days, entries.Sum(e => e.InterestDays ?? 0));
        Assert.Single(entries, e => e.PrincipalAmount > 0);
    }

    [Fact]
    public async Task GeneratePaymentPlan_UsesPaidInDateAndAnnualBulletModel()
    {
        var sub = await CreateSubscriptionAsync(new DateTime(2026, 4, 25, 12, 6, 43, DateTimeKind.Utc));

        var plan = await _service.GeneratePaymentPlanAsync(sub.Id);

        var entries = plan.Entries.OrderBy(e => e.PeriodNumber).ToList();
        Assert.Equal(3, entries.Count);
        Assert.Equal(new DateTime(2026, 12, 31), entries[0].DueDate);
        Assert.Equal(new DateTime(2028, 4, 25), entries[2].DueDate);
        Assert.All(entries, e => Assert.Equal(plan.Id, e.LoanPaymentPlanId));
    }

    [Fact]
    public async Task GetEntriesByYear_ReturnsPaidAndOpenEntriesOfThatYearOnly()
    {
        var sub = await CreateSubscriptionAsync(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        var plan = await _service.GeneratePaymentPlanAsync(sub.Id);
        var first = plan.Entries.OrderBy(e => e.PeriodNumber).First();
        await _service.MarkEntryAsPaidAsync(first.Id);

        var entries2026 = await _service.GetEntriesByYearAsync(2026);
        var entries2028 = await _service.GetEntriesByYearAsync(2028);

        var paid = Assert.Single(entries2026);
        Assert.Equal(PaymentPlanEntryStatus.Paid, paid.Status);
        var final = Assert.Single(entries2028);
        Assert.True(final.PrincipalAmount > 0 && final.InterestAmount > 0);
    }

    [Fact]
    public async Task RegeneratePaymentPlans_SkipsPlansWithPaidEntries()
    {
        var untouched = await CreateSubscriptionAsync(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        var paidPlan = await _service.GeneratePaymentPlanAsync(untouched.Id);
        await _service.MarkEntryAsPaidAsync(paidPlan.Entries.First().Id);

        await CreateSubscriptionAsync(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateSubscriptionAsync(null);

        var count = await _service.RegeneratePaymentPlansAsync();

        Assert.Equal(1, count);
        Assert.Equal(paidPlan.Id, (await _service.GetPaymentPlanBySubscriptionIdAsync(untouched.Id))!.Id);
    }

    private async Task<LoanSubscription> CreateSubscriptionAsync(DateTime? paidInDate)
    {
        var offer = await _context.LoanOffers.FirstOrDefaultAsync();
        if (offer == null)
        {
            var project = new LoanProject { Title = "Stausee", ProjectNumber = "P1", FinancingAmount = 100000m };
            offer = new LoanOffer
            {
                LoanProject = project,
                Title = "2 Jahre",
                InterestRate = 0.04m,
                TermMonths = 24,
                PaymentInterval = PaymentInterval.Annual,
                RepaymentType = RepaymentType.BulletLoan
            };
            _context.LoanOffers.Add(offer);
        }

        var n = await _context.Members.CountAsync() + 1;
        var member = new Member { MemberNumber = $"M{n:000}", FirstName = "Test", LastName = $"Member{n}", Email = $"m{n}@example.com" };
        var sub = new LoanSubscription
        {
            LoanOffer = offer,
            Member = member,
            SubscriptionNumber = $"LS{n:0000}",
            SubscriptionDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 5000m,
            PaidInDate = paidInDate,
            BankAccountHolder = "Test",
            IBAN = "DE89370400440532013000"
        };
        _context.LoanSubscriptions.Add(sub);
        await _context.SaveChangesAsync();
        return sub;
    }
}
