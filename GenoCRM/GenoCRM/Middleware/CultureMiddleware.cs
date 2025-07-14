using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;

namespace GenoCRM.Middleware;

public class CultureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CultureMiddleware> _logger;

    public CultureMiddleware(RequestDelegate next, ILogger<CultureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for culture parameter in query string
        if (context.Request.Query.TryGetValue("culture", out var cultureQuery))
        {
            var culture = cultureQuery.ToString();
            if (IsValidCulture(culture))
            {
                try
                {
                    // Set the culture cookie
                    context.Response.Cookies.Append(
                        CookieRequestCultureProvider.DefaultCookieName,
                        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                        new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddYears(1),
                            IsEssential = true,
                            SameSite = SameSiteMode.Lax
                        });

                    // Set the current thread culture
                    var cultureInfo = new CultureInfo(culture);
                    CultureInfo.CurrentCulture = cultureInfo;
                    CultureInfo.CurrentUICulture = cultureInfo;

                    _logger.LogInformation("Culture set to: {Culture} via query parameter", culture);

                    // Redirect to the same URL without the culture parameter to clean up the URL
                    var url = context.Request.GetDisplayUrl();
                    var uri = new Uri(url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    query.Remove("culture");
                    
                    var uriBuilder = new UriBuilder(uri)
                    {
                        Query = query.ToString()
                    };
                    
                    context.Response.Redirect(uriBuilder.ToString());
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting culture from query parameter: {Culture}", culture);
                }
            }
        }

        await _next(context);
    }

    private static bool IsValidCulture(string culture)
    {
        var supportedCultures = new[] { "en", "de" };
        return supportedCultures.Contains(culture);
    }
}