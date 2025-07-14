using System.Globalization;

namespace GenoCRM.Services.Localization;

public interface IFormattingService
{
    string FormatDate(DateTime date);
    string FormatDate(DateTime? date);
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

    public string FormatCurrency(decimal amount)
    {
        var culture = _cultureService.GetCurrentCulture();
        
        // Use Euro for German culture, otherwise use the system default
        var currencySymbol = culture.Name == "de" ? "€" : culture.NumberFormat.CurrencySymbol;
        
        if (culture.Name == "de")
        {
            // German format: 1.234,56 €
            return $"{amount:N2} €";
        }
        
        // Default format (English): $1,234.56
        return amount.ToString("C", culture);
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