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
using GenoCRM.Services.Storage;
using GenoCRM.Services.PDF;
using GenoCRM.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Serilog;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

// Load .env file if it exists (for development)
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

// Map environment variables to configuration format
// Nextcloud Integration
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_BASE_URL")))
    Environment.SetEnvironmentVariable("Nextcloud__BaseUrl", Environment.GetEnvironmentVariable("NEXTCLOUD_BASE_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_WEBDAV_URL")))
    Environment.SetEnvironmentVariable("Nextcloud__WebDAVUrl", Environment.GetEnvironmentVariable("NEXTCLOUD_WEBDAV_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_USERNAME")))
    Environment.SetEnvironmentVariable("Nextcloud__Username", Environment.GetEnvironmentVariable("NEXTCLOUD_USERNAME"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_PASSWORD")))
    Environment.SetEnvironmentVariable("Nextcloud__Password", Environment.GetEnvironmentVariable("NEXTCLOUD_PASSWORD"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_DOCUMENTS_PATH")))
    Environment.SetEnvironmentVariable("Nextcloud__DocumentsPath", Environment.GetEnvironmentVariable("NEXTCLOUD_DOCUMENTS_PATH"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_MEMBER_DOCUMENTS_PATH")))
    Environment.SetEnvironmentVariable("Nextcloud__MemberDocumentsPath", Environment.GetEnvironmentVariable("NEXTCLOUD_MEMBER_DOCUMENTS_PATH"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_SHARE_DOCUMENTS_PATH")))
    Environment.SetEnvironmentVariable("Nextcloud__ShareDocumentsPath", Environment.GetEnvironmentVariable("NEXTCLOUD_SHARE_DOCUMENTS_PATH"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_GENERATED_DOCUMENTS_PATH")))
    Environment.SetEnvironmentVariable("Nextcloud__GeneratedDocumentsPath", Environment.GetEnvironmentVariable("NEXTCLOUD_GENERATED_DOCUMENTS_PATH"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_MAX_FILE_SIZE")))
    Environment.SetEnvironmentVariable("Nextcloud__MaxFileSize", Environment.GetEnvironmentVariable("NEXTCLOUD_MAX_FILE_SIZE"));

// Nextcloud OAuth Authentication
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_BASE_URL")))
    Environment.SetEnvironmentVariable("NextcloudAuth__BaseUrl", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_BASE_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_AUTHORIZE_ENDPOINT")))
    Environment.SetEnvironmentVariable("NextcloudAuth__AuthorizeEndpoint", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_AUTHORIZE_ENDPOINT"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_TOKEN_ENDPOINT")))
    Environment.SetEnvironmentVariable("NextcloudAuth__TokenEndpoint", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_TOKEN_ENDPOINT"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_USER_INFO_ENDPOINT")))
    Environment.SetEnvironmentVariable("NextcloudAuth__UserInfoEndpoint", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_USER_INFO_ENDPOINT"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_CLIENT_ID")))
    Environment.SetEnvironmentVariable("NextcloudAuth__ClientId", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_CLIENT_ID"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_CLIENT_SECRET")))
    Environment.SetEnvironmentVariable("NextcloudAuth__ClientSecret", Environment.GetEnvironmentVariable("NEXTCLOUD_AUTH_CLIENT_SECRET"));

// SMTP Email Configuration
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_HOST")))
    Environment.SetEnvironmentVariable("Smtp__Host", Environment.GetEnvironmentVariable("SMTP_HOST"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_PORT")))
    Environment.SetEnvironmentVariable("Smtp__Port", Environment.GetEnvironmentVariable("SMTP_PORT"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_USERNAME")))
    Environment.SetEnvironmentVariable("Smtp__Username", Environment.GetEnvironmentVariable("SMTP_USERNAME"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_PASSWORD")))
    Environment.SetEnvironmentVariable("Smtp__Password", Environment.GetEnvironmentVariable("SMTP_PASSWORD"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL")))
    Environment.SetEnvironmentVariable("Smtp__EnableSsl", Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")))
    Environment.SetEnvironmentVariable("Smtp__FromEmail", Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_FROM_NAME")))
    Environment.SetEnvironmentVariable("Smtp__FromName", Environment.GetEnvironmentVariable("SMTP_FROM_NAME"));

// WhatsApp Business API
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN")))
    Environment.SetEnvironmentVariable("WhatsApp__AccessToken", Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WHATSAPP_API_URL")))
    Environment.SetEnvironmentVariable("WhatsApp__ApiUrl", Environment.GetEnvironmentVariable("WHATSAPP_API_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID")))
    Environment.SetEnvironmentVariable("WhatsApp__PhoneNumberId", Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WHATSAPP_WEBHOOK_VERIFY_TOKEN")))
    Environment.SetEnvironmentVariable("WhatsApp__WebhookVerifyToken", Environment.GetEnvironmentVariable("WHATSAPP_WEBHOOK_VERIFY_TOKEN"));

// SMS Provider API
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_API_KEY")))
    Environment.SetEnvironmentVariable("Sms__ApiKey", Environment.GetEnvironmentVariable("SMS_API_KEY"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_API_URL")))
    Environment.SetEnvironmentVariable("Sms__ApiUrl", Environment.GetEnvironmentVariable("SMS_API_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_STATUS_URL")))
    Environment.SetEnvironmentVariable("Sms__StatusUrl", Environment.GetEnvironmentVariable("SMS_STATUS_URL"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_FROM_NUMBER")))
    Environment.SetEnvironmentVariable("Sms__FromNumber", Environment.GetEnvironmentVariable("SMS_FROM_NUMBER"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_DEFAULT_RATE")))
    Environment.SetEnvironmentVariable("Sms__DefaultRate", Environment.GetEnvironmentVariable("SMS_DEFAULT_RATE"));
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMS_PROVIDER")))
    Environment.SetEnvironmentVariable("Sms__Provider", Environment.GetEnvironmentVariable("SMS_PROVIDER"));

// Database Configuration
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")))
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING"));

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

// Database configuration - PostgreSQL only
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<GenoDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    
    // In development, suppress the pending model changes warning to allow more flexible migration handling
    if (builder.Environment.IsDevelopment())
    {
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }
});

// Business services
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IShareService, ShareService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDividendService, DividendService>();
builder.Services.AddScoped<IFiscalYearService, FiscalYearService>();
builder.Services.AddScoped<IShareTransferService, ShareTransferService>();
builder.Services.AddScoped<IShareApprovalService, ShareApprovalService>();
builder.Services.AddScoped<IShareConsolidationService, ShareConsolidationService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

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
builder.Services.AddHttpClient<INextcloudDocumentService, NextcloudDocumentService>();
builder.Services.AddScoped<INextcloudDocumentService, NextcloudDocumentService>();

// PDF generation services
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();

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

// Apply database migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<GenoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Applying database migrations...");
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                pendingMigrations.Count(), string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("Database is up to date, no migrations to apply.");
        }
        
        // Seed data in development
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("Seeding development data...");
            //await GenoCRM.Data.SeedData.SeedAsync(context);
            await GenoCRM.Data.MessagingSeedData.SeedAsync(context);
            logger.LogInformation("Development data seeded successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations or seeding data.");
        throw;
    }
}

app.Run();