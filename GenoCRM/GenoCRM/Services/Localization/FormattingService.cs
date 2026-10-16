using System.Globalization;

namespace GenoCRM.Services.Localization;

public interface IFormattingService
{
    string FormatDate(DateTime date);
    string FormatDate(DateTime? date);
    string FormatDateTime(DateTime dateTime);
    string FormatDateTime(DateTime? dateTime);
    string FormatCurrency(decimal amount);
    string FormatNumber(decimal number);
    string FormatPercentage(decimal percentage);
}

public class FormattingService : IFormattingService
{
    private readonly ICultureService _cultureService;

    public FormattingService(ICultureService cultureService)
    {
        _cultureService = cultureService;
    }

    public string FormatDate(DateTime date)
    {
        var culture = _cultureService.GetCurrentCulture();
        return date.ToString("d", culture);
    }

    public string FormatDate(DateTime? date)
    {
        if (!date.HasValue) return string.Empty;
        return FormatDate(date.Value);
    }

    private static readonly TimeZoneInfo DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public string FormatDateTime(DateTime dateTime)
    {
        var culture = _cultureService.GetCurrentCulture();
        // Timestamps are stored in UTC (and come back from Postgres with Kind=Unspecified);
        // show them in the cooperative's local time
        var utc = dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, DisplayTimeZone);
        return local.ToString("g", culture);
    }

    public string FormatDateTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return string.Empty;
        return FormatDateTime(dateTime.Value);
    }

    public string FormatCurrency(decimal amount)
    {
        var culture = _cultureService.GetCurrentCulture();

        // Amounts are always euros, whatever the UI language. The neutral "en" culture has no
        // euro symbol (it yields "$" or "¤" depending on the platform), so set it explicitly.
        if (culture.TwoLetterISOLanguageName == "de")
        {
            // German format: 1.234,56 €
            return $"{amount.ToString("N2", culture)} €";
        }

        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = "€";
        return amount.ToString("C", format);
    }

    public string FormatNumber(decimal number)
    {
        var culture = _cultureService.GetCurrentCulture();
        return number.ToString("N2", culture);
    }

    public string FormatPercentage(decimal percentage)
    {
        var culture = _cultureService.GetCurrentCulture();
        return percentage.ToString("P2", culture);
    }
}