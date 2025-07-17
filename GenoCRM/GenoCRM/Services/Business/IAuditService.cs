using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IAuditService
{
    Task LogActionAsync(
        string userName,
        AuditAction action,
        string entityType,
        string entityId,
        string entityDescription,
        string? permission = null,
        object? changes = null,
        string? ipAddress = null,
        string? userAgent = null);
    
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? entityType = null,
        string? userName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);
    
    Task<int> GetAuditLogCountAsync(
        string? entityType = null,
        string? userName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);
    
    Task<IEnumerable<AuditLog>> GetEntityAuditHistoryAsync(string entityType, string entityId);
    
    Task<IEnumerable<string>> GetAuditedEntityTypesAsync();
    
    Task<IEnumerable<string>> GetAuditedUsersAsync();
}