using GenoCRM.Components;
using GenoCRM.Data;
using GenoCRM.Services.Business;
using GenoCRM.Services.Business.Messaging;
using GenoCRM.Services.Integration;
using GenoCRM.Services.Authentication;
using GenoCRM.Services.Authorization;
using GenoCRM.Services.Configuration;
using GenoCRM.Services.Localization;
using GenoCRM.Services.UI;
using GenoCRM.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Serilog;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure localization
builder.Services.AddLocalization();

// Configure supported cultures
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("de")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Configure culture providers (in order of priority)
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

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
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDividendService, DividendService>();
builder.Services.AddScoped<IFiscalYearService, FiscalYearService>();
builder.Services.AddScoped<IShareTransferService, ShareTransferService>();
builder.Services.AddScoped<IShareApprovalService, ShareApprovalService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Messaging providers
builder.Services.AddScoped<IEmailProvider, SmtpEmailProvider>();
builder.Services.AddScoped<IWhatsAppProvider, WhatsAppProvider>();
builder.Services.AddScoped<ISmsProvider, SmsProvider>();

// Configure messaging settings
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection("Sms"));

// HTTP clients for messaging providers
builder.Services.AddHttpClient<IWhatsAppProvider, WhatsAppProvider>();
builder.Services.AddHttpClient<ISmsProvider, SmsProvider>();

// Integration services
builder.Services.AddHttpClient<INextcloudService, NextcloudService>();
builder.Services.AddScoped<INextcloudService, NextcloudService>();

// Authentication services
builder.Services.AddHttpClient<INextcloudAuthService, NextcloudAuthService>();
builder.Services.AddScoped<INextcloudAuthService, NextcloudAuthService>();

// Configuration services
builder.Services.AddSingleton<IGroupPermissionService, GroupPermissionService>();

// Localization services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICultureService, CultureService>();
builder.Services.AddScoped<IFormattingService, FormattingService>();
builder.Services.AddScoped<ICountryService, CountryService>();

// UI services
builder.Services.AddScoped<IModalService, ModalService>();

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

// Use culture middleware before request localization
app.UseMiddleware<CultureMiddleware>();

// Use request localization
app.UseRequestLocalization();

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
        //await GenoCRM.Data.SeedData.SeedAsync(context);
        await GenoCRM.Data.MessagingSeedData.SeedAsync(context);
    }
}

app.Run();