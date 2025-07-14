using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace GenoCRM.Services.Localization;

public interface ICultureService
{
    CultureInfo GetCurrentCulture();
    void SetCulture(string culture);
    IEnumerable<CultureInfo> GetSupportedCultures();
    string GetCultureDisplayName(string culture);
}

public class CultureService : ICultureService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CultureService> _logger;
    
    private static readonly CultureInfo[] SupportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("de")
    };

    public CultureService(IHttpContextAccessor httpContextAccessor, ILogger<CultureService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public CultureInfo GetCurrentCulture()
    {
        return CultureInfo.CurrentUICulture;
    }

    public void SetCulture(string culture)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            // Validate the culture
            if (!SupportedCultures.Any(c => c.Name == culture))
            {
                _logger.LogWarning("Attempted to set unsupported culture: {Culture}", culture);
                return;
            }

            // Set culture cookie
            httpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax
                });

            _logger.LogInformation("Culture set to: {Culture}", culture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting culture to: {Culture}", culture);
        }
    }

    public IEnumerable<CultureInfo> GetSupportedCultures()
    {
        return SupportedCultures;
    }

    public string GetCultureDisplayName(string culture)
    {
        return culture switch
        {
            "en" => "English",
            "de" => "Deutsch",
            _ => culture
        };
    }
}