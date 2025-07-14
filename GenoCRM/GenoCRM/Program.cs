using GenoCRM.Components;
using GenoCRM.Data;
using GenoCRM.Services.Business;
using GenoCRM.Services.Integration;
using Microsoft.EntityFrameworkCore;
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

// Authentication (commented out for now - will be configured later)
// builder.Services.AddAuthentication("Nextcloud")
//     .AddOAuth("Nextcloud", options =>
//     {
//         options.AuthorizationEndpoint = builder.Configuration["Nextcloud:AuthorizationEndpoint"];
//         options.TokenEndpoint = builder.Configuration["Nextcloud:TokenEndpoint"];
//         options.ClientId = builder.Configuration["Nextcloud:ClientId"];
//         options.ClientSecret = builder.Configuration["Nextcloud:ClientSecret"];
//     });

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

app.UseHttpsRedirection();


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