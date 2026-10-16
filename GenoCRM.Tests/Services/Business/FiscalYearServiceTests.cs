using Microsoft.Extensions.Configuration;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class FiscalYearServiceTests
{
    private static FiscalYearService Create() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());

    [Fact]
    public void GetFiscalYearEnd_IsInclusiveLastDayOfYear()
    {
        Assert.Equal(new DateTime(2025, 12, 31), Create().GetFiscalYearEnd(2025));
    }

    [Fact]
    public void CanProcessOffboarding_IsTrueOnceTheFiscalYearOfTheMembershipEndHasPassed()
    {
        var service = Create();
        var lastYear = new DateTime(DateTime.Today.Year - 1, 12, 31);

        Assert.True(service.CanProcessOffboarding(lastYear));
        Assert.True(service.CanProcessOffboarding(lastYear.AddMonths(-6)));
    }

    [Fact]
    public void CanProcessOffboarding_IsFalseWhileTheFiscalYearOfTheMembershipEndIsStillRunning()
    {
        var service = Create();
        var thisYearEnd = new DateTime(DateTime.Today.Year, 12, 31);

        Assert.False(service.CanProcessOffboarding(thisYearEnd));
        Assert.False(service.CanProcessOffboarding(DateTime.Today));
        Assert.False(service.CanProcessOffboarding(thisYearEnd.AddYears(1)));
    }
}
