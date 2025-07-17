using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using System.Text.Json;

namespace GenoCRM.Services.Business;

public class AuditService : IAuditService
{
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
            var auditLog = new AuditLog
            {
                UserName = userName,
                Action = action.ToString(),
                EntityType = entityType,
                EntityId = entityId,
                EntityDescription = entityDescription,
                Permission = permission,
                Changes = changes != null ? JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true }) : null,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

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
                query = query.Where(a => a.Timestamp <= toDate.Value);
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
                query = query.Where(a => a.Timestamp <= toDate.Value);
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