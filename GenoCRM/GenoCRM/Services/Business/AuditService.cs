using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GenoCRM.Services.Business;

public class AuditService : IAuditService
{
    private const int MaxEntityDescriptionLength = 200;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    // Keep umlauts, '+' and similar readable in stored audit JSON instead of \uXXXX escapes
    private static readonly JsonSerializerOptions ChangesJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly GenoDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(GenoDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogActionAsync(
        string userName,
        AuditAction action,
        string entityType,
        string entityId,
        string entityDescription,
        string? permission = null,
        object? changes = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        try
        {
            _logger.LogInformation("Attempting to log audit entry: User={UserName}, Action={Action}, EntityType={EntityType}, EntityId={EntityId}", 
                userName, action, entityType, entityId);
            var auditLog = new AuditLog
            {
                UserName = userName,
                Action = action.ToString(),
                EntityType = entityType,
                EntityId = entityId,
                EntityDescription = Truncate(entityDescription, MaxEntityDescriptionLength),
                Permission = permission,
                Changes = changes != null ? JsonSerializer.Serialize(changes, ChangesJsonOptions) : null,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.AuditLogs.Add(auditLog);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Detach the failed row, otherwise every later SaveChangesAsync on this (scoped) context retries it and fails too
                _context.Entry(auditLog).State = EntityState.Detached;
                throw;
            }

            _logger.LogInformation("Audit log created: {Action} on {EntityType} {EntityId} by {UserName}", 
                action, entityType, entityId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for {Action} on {EntityType} {EntityId} by {UserName}", 
                action, entityType, entityId, userName);
            
            // Don't throw - audit logging failure should not break business operations
        }
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? entityType = null,
        string? userName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (!string.IsNullOrEmpty(userName))
            {
                query = query.Where(a => a.UserName.Contains(userName));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                // toDate is a calendar day: include everything up to the end of that day
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < toExclusive);
            }

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            throw;
        }
    }

    public async Task<int> GetAuditLogCountAsync(
        string? entityType = null,
        string? userName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (!string.IsNullOrEmpty(userName))
            {
                query = query.Where(a => a.UserName.Contains(userName));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                // toDate is a calendar day: include everything up to the end of that day
                var toExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < toExclusive);
            }

            return await query.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting audit logs");
            throw;
        }
    }

    public async Task<IEnumerable<AuditLog>> GetEntityAuditHistoryAsync(string entityType, string entityId)
    {
        try
        {
            return await _context.AuditLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit history for {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetAuditedEntityTypesAsync()
    {
        try
        {
            return await _context.AuditLogs
                .Select(a => a.EntityType)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audited entity types");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetAuditedUsersAsync()
    {
        try
        {
            return await _context.AuditLogs
                .Select(a => a.UserName)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audited users");
            throw;
        }
    }
}