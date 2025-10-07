using GenoCRM.Data;
using GenoCRM.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GenoCRM.Services.Integration;

/// <summary>
/// Background service that periodically syncs all active members to Listmonk
/// </summary>
public class ListmonkSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ListmonkSyncBackgroundService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);

    public ListmonkSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ListmonkSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Listmonk Sync Background Service is starting");

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllMembersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing members to Listmonk");
            }

            // Wait for the next sync interval
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        _logger.LogInformation("Listmonk Sync Background Service is stopping");
    }

    private async Task SyncAllMembersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GenoDbContext>();
        var listmonkService = scope.ServiceProvider.GetRequiredService<IListmonkService>();

        _logger.LogInformation("Starting Listmonk sync for all members");

        try
        {
            // Get all members with valid email addresses
            var members = await context.Members
                .Where(m => !string.IsNullOrEmpty(m.Email))
                .ToListAsync(cancellationToken);

            var syncedCount = 0;
            var errorCount = 0;

            foreach (var member in members)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Listmonk sync cancelled");
                    break;
                }

                try
                {
                    await listmonkService.SyncMemberAsync(member);
                    syncedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing member {MemberId} to Listmonk", member.Id);
                    errorCount++;
                }

                // Small delay between requests to avoid overwhelming the Listmonk API
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            _logger.LogInformation(
                "Listmonk sync completed: {SyncedCount} members synced, {ErrorCount} errors",
                syncedCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Listmonk sync operation");
            throw;
        }
    }
}
