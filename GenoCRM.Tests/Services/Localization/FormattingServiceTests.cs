using System.Globalization;
using Moq;
using GenoCRM.Services.Localization;
using Xunit;

namespace GenoCRM.Tests.Services.Localization;

public class FormattingServiceTests
{
    private static FormattingService Create(string culture)
    {
        var cultureService = new Mock<ICultureService>();
        cultureService.Setup(c => c.GetCurrentCulture()).Returns(new CultureInfo(culture));
        return new FormattingService(cultureService.Object);
    }

    [Fact]
    public void FormatCurrency_German_UsesEuroWithGermanSeparators()
    {
        Assert.Equal("1.234,56 €", Create("de").FormatCurrency(1234.56m));
    }

    [Fact]
    public void FormatCurrency_English_StillShowsEuro()
    {
        var text = Create("en").FormatCurrency(1234.56m);

        Assert.Contains("€", text);
        Assert.DoesNotContain("$", text);
        Assert.DoesNotContain("¤", text);
        Assert.Contains("1,234.56", text);
    }

    [Fact]
    public void FormatDateTime_ConvertsStoredUtcToGermanLocalTime()
    {
        // Stored timestamps are UTC (often with Kind=Unspecified when read back from Postgres)
        var summerNoonUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var text = Create("de").FormatDateTime(summerNoonUtc);

        Assert.Contains("14:00", text);
        Assert.Contains("01.07.2026", text);
    }
}
