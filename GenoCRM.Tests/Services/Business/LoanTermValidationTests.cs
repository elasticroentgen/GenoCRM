using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class LoanTermValidationTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly LoanPaymentPlanService _planService;
    private readonly LoanOfferService _offerService;

    public LoanTermValidationTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _planService = new LoanPaymentPlanService(_context, new Mock<ILogger<LoanPaymentPlanService>>().Object);
        _offerService = new LoanOfferService(_context, new Mock<ILogger<LoanOfferService>>().Object);
    }

    private async Task<LoanSubscription> SeedSubscriptionAsync(int termMonths, PaymentInterval interval, RepaymentType type = RepaymentType.Annuity)
    {
        var project = new LoanProject { Title = "P", ProjectNumber = "LP001", StartDate = DateTime.UtcNow, FinancingAmount = 100000m, Status = LoanProjectStatus.Active };
        _context.LoanProjects.Add(project);
        var member = new Member { MemberNumber = "M001", FirstName = "A", LastName = "B", Email = "a@b.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        var offer = new LoanOffer { LoanProjectId = project.Id, Title = "O", InterestRate = 0.03m, TermMonths = termMonths, PaymentInterval = interval, RepaymentType = type };
        _context.LoanOffers.Add(offer);
        await _context.SaveChangesAsync();
        var sub = new LoanSubscription { LoanOfferId = offer.Id, MemberId = member.Id, SubscriptionNumber = "S001", SubscriptionDate = DateTime.UtcNow, Amount = 10000m, PaidInDate = DateTime.UtcNow, Status = LoanSubscriptionStatus.Active };
        _context.LoanSubscriptions.Add(sub);
        await _context.SaveChangesAsync();
        return sub;
    }

    [Theory]
    [InlineData(18, PaymentInterval.Annual)]
    [InlineData(6, PaymentInterval.Annual)]
    [InlineData(0, PaymentInterval.Monthly)]
    [InlineData(5, PaymentInterval.Quarterly)]
    public async Task GeneratePaymentPlan_RejectsTermsThatDoNotFitThePaymentInterval(int termMonths, PaymentInterval interval)
    {
        var sub = await SeedSubscriptionAsync(termMonths, interval);

        await Assert.ThrowsAsync<ArgumentException>(() => _planService.GeneratePaymentPlanAsync(sub.Id));
    }

    [Theory]
    [InlineData(18, PaymentInterval.SemiAnnual, 3)]
    [InlineData(24, PaymentInterval.Annual, 2)]
    [InlineData(9, PaymentInterval.Quarterly, 3)]
    public async Task GeneratePaymentPlan_CreatesOneEntryPerPeriod(int termMonths, PaymentInterval interval, int expectedPeriods)
    {
        var sub = await SeedSubscriptionAsync(termMonths, interval, RepaymentType.BulletLoan);

        var plan = await _planService.GeneratePaymentPlanAsync(sub.Id);

        Assert.Equal(expectedPeriods, plan.Entries.Count);
        Assert.Equal(10000m, plan.Entries.Last().PrincipalAmount);
    }

    [Fact]
    public async Task CreateOffer_RejectsTermThatDoesNotFitThePaymentInterval()
    {
        var project = new LoanProject { Title = "P", ProjectNumber = "LP001", StartDate = DateTime.UtcNow, FinancingAmount = 1m, Status = LoanProjectStatus.Active };
        _context.LoanProjects.Add(project);
        await _context.SaveChangesAsync();
        var offer = new LoanOffer { LoanProjectId = project.Id, Title = "O", InterestRate = 0.03m, TermMonths = 18, PaymentInterval = PaymentInterval.Annual };

        await Assert.ThrowsAsync<ArgumentException>(() => _offerService.CreateOfferAsync(offer));
    }

    [Fact]
    public async Task UpdateOffer_RejectsTermThatDoesNotFitThePaymentInterval_AndCopiesGracePeriod()
    {
        var project = new LoanProject { Title = "P", ProjectNumber = "LP001", StartDate = DateTime.UtcNow, FinancingAmount = 1m, Status = LoanProjectStatus.Active };
        _context.LoanProjects.Add(project);
        await _context.SaveChangesAsync();
        var offer = new LoanOffer { LoanProjectId = project.Id, Title = "O", InterestRate = 0.03m, TermMonths = 12, PaymentInterval = PaymentInterval.Annual };
        _context.LoanOffers.Add(offer);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _offerService.UpdateOfferAsync(new LoanOffer { Id = offer.Id, Title = "O", TermMonths = 18, PaymentInterval = PaymentInterval.Annual }));

        await _offerService.UpdateOfferAsync(new LoanOffer { Id = offer.Id, Title = "O", TermMonths = 24, PaymentInterval = PaymentInterval.Annual, GracePeriodMonths = 6 });
        Assert.Equal(6, offer.GracePeriodMonths);
    }

    public void Dispose() => _context.Dispose();
}
