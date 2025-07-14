using GenoCRM.Components;
using GenoCRM.Data;
using GenoCRM.Services.Business;
using GenoCRM.Services.Integration;
using GenoCRM.Services.Authentication;
using GenoCRM.Services.Authorization;
using GenoCRM.Services.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Configure Blazor circuit options for detailed errors in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
    {
        options.DetailedErrors = true;
    });
}

// Add authentication state provider
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, 
    Microsoft.AspNetCore.Components.Server.ServerAuthenticationStateProvider>();

// Add API controllers
builder.Services.AddControllers();

// Database configuration
builder.Services.AddDbContext<GenoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Business services
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IDividendService, DividendService>();

// Integration services
builder.Services.AddHttpClient<INextcloudService, NextcloudService>();
builder.Services.AddScoped<INextcloudService, NextcloudService>();

// Authentication services
builder.Services.AddHttpClient<INextcloudAuthService, NextcloudAuthService>();
builder.Services.AddScoped<INextcloudAuthService, NextcloudAuthService>();

// Configuration services
builder.Services.AddSingleton<IGroupPermissionService, GroupPermissionService>();

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Cookies";
    options.DefaultSignInScheme = "Cookies";
    options.DefaultChallengeScheme = "Nextcloud";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
})
.AddOAuth("Nextcloud", options =>
{
    options.AuthorizationEndpoint = builder.Configuration["NextcloudAuth:AuthorizeEndpoint"]!;
    options.TokenEndpoint = builder.Configuration["NextcloudAuth:TokenEndpoint"]!;
    options.ClientId = builder.Configuration["NextcloudAuth:ClientId"]!;
    options.ClientSecret = builder.Configuration["NextcloudAuth:ClientSecret"]!;
    options.CallbackPath = "/signin-nextcloud";
    
    // Add scopes
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    
    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            // Get user info and groups from Nextcloud
            var authService = context.HttpContext.RequestServices.GetRequiredService<INextcloudAuthService>();
            
            var nextcloudUser = await authService.GetUserInfoAsync(context.AccessToken!);
            if (nextcloudUser != null)
            {
                var groups = await authService.GetUserGroupsAsync(context.AccessToken!, nextcloudUser.Id);
                var user = await authService.SyncUserAsync(nextcloudUser, groups);
                
                // Create claims principal
                var principal = authService.CreateClaimsPrincipal(user);
                context.Principal = principal;
                
                // Sign in the user with the cookie scheme
                await context.HttpContext.SignInAsync("Cookies", principal);
            }
        }
    };
});

// Configure Authorization
builder.Services.AddAuthorization(AuthorizationPolicies.ConfigurePolicies);

// Authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, GroupAuthorizationHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Configure CSP middleware for development to allow Blazor to work
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Remove("Content-Security-Policy");
        await next();
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GenoCRM.Client._Imports).Assembly);

// Map API controllers
app.MapControllers();

// Seed database in development
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<GenoDbContext>();
        await context.Database.EnsureCreatedAsync();
        await GenoCRM.Data.SeedData.SeedAsync(context);
    }
}

app.Run();