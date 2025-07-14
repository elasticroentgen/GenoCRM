using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenoCRM.Controllers;

[Microsoft.AspNetCore.Mvc.Route("")]
public class AuthController : Controller
{
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(returnUrl ?? "/");
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/"
        };

        return Challenge(props, "Nextcloud");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        
        _logger.LogInformation("User {UserId} logged out", User.Identity?.Name);
        
        return Redirect("/");
    }

}