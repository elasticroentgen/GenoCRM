using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using FluentAssertions;

namespace GenoCRM.Tests.Utilities;

/// <summary>
/// Helper utilities for testing audit functionality
/// </summary>
public static class AuditTestHelpers
{
    /// <summary>
    /// Verifies that an audit log entry exists with the specified criteria
    /// </summary>
    public static async Task<AuditLog> VerifyAuditLogExistsAsync(
        GenoDbContext context,
        string userName,
        AuditAction action,
        string entityType,
        string? entityId = null,
        string? permission = null)
    {
        var query = context.AuditLogs
            .Where(a => a.UserName == userName)
            .Where(a => a.Action == action.ToString())
            .Where(a => a.EntityType == entityType);

        if (entityId != null)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (permission != null)
        {
            query = query.Where(a => a.Permission == permission);
        }

        var auditLog = await query.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull($"Expected audit log not found for user '{userName}', action '{action}', entity type '{entityType}'");
        
        return auditLog!;
    }

    /// <summary>
    /// Verifies the basic properties of an audit log entry
    /// </summary>
    public static void VerifyAuditLogProperties(
        AuditLog auditLog,
        string expectedUserName,
        AuditAction expectedAction,
        string expectedEntityType,
        string? expectedEntityId = null,
        string? expectedPermission = null,
        bool shouldHaveChanges = false,
        bool shouldHaveIpAddress = false,
        bool shouldHaveUserAgent = false)
    {
        auditLog.UserName.Should().Be(expectedUserName);
        auditLog.Action.Should().Be(expectedAction.ToString());
        auditLog.EntityType.Should().Be(expectedEntityType);
        auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));

        if (expectedEntityId != null)
        {
            auditLog.EntityId.Should().Be(expectedEntityId);
        }

        if (expectedPermission != null)
        {
            auditLog.Permission.Should().Be(expectedPermission);
        }

        if (shouldHaveChanges)
        {
            auditLog.Changes.Should().NotBeNull();
            auditLog.Changes.Should().NotBeEmpty();
        }

        if (shouldHaveIpAddress)
        {
            auditLog.IpAddress.Should().NotBeNull();
            auditLog.IpAddress.Should().NotBeEmpty();
        }

        if (shouldHaveUserAgent)
        {
            auditLog.UserAgent.Should().NotBeNull();
            auditLog.UserAgent.Should().NotBeEmpty();
        }
    }

    /// <summary>
    /// Gets all audit logs for a specific entity
    /// </summary>
    public static async Task<List<AuditLog>> GetEntityAuditHistoryAsync(
        GenoDbContext context,
        string entityType,
        string entityId)
    {
        return await context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    /// <summary>
    /// Verifies that audit logs are created in the expected chronological order
    /// </summary>
    public static void VerifyAuditLogOrder(List<AuditLog> auditLogs)
    {
        auditLogs.Should().NotBeEmpty();
        
        for (int i = 1; i < auditLogs.Count; i++)
        {
            auditLogs[i - 1].Timestamp.Should().BeAfter(auditLogs[i].Timestamp,
                "Audit logs should be ordered by timestamp descending (most recent first)");
        }
    }

    /// <summary>
    /// Verifies that no audit logs exist for the specified criteria
    /// </summary>
    public static async Task VerifyNoAuditLogsExistAsync(
        GenoDbContext context,
        string? userName = null,
        AuditAction? action = null,
        string? entityType = null,
        string? entityId = null)
    {
        var query = context.AuditLogs.AsQueryable();

        if (userName != null)
        {
            query = query.Where(a => a.UserName == userName);
        }

        if (action.HasValue)
        {
            query = query.Where(a => a.Action == action.Value.ToString());
        }

        if (entityType != null)
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (entityId != null)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        var count = await query.CountAsync();
        count.Should().Be(0, "Expected no audit logs to exist for the specified criteria");
    }

    /// <summary>
    /// Verifies that changes in an audit log contain the expected values
    /// </summary>
    public static void VerifyAuditLogChanges(AuditLog auditLog, params string[] expectedValues)
    {
        auditLog.Changes.Should().NotBeNull();
        auditLog.Changes.Should().NotBeEmpty();

        foreach (var expectedValue in expectedValues)
        {
            auditLog.Changes.Should().Contain(expectedValue,
                $"Expected audit log changes to contain '{expectedValue}'");
        }
    }

    /// <summary>
    /// Creates a comprehensive verification of an audit log entry
    /// </summary>
    public static async Task<AuditLog> VerifyCompleteAuditLogAsync(
        GenoDbContext context,
        string userName,
        AuditAction action,
        string entityType,
        string entityId,
        string expectedEntityDescription,
        string? expectedPermission = null,
        string[]? expectedChanges = null,
        string? expectedIpAddress = null,
        string? expectedUserAgent = null)
    {
        var auditLog = await VerifyAuditLogExistsAsync(context, userName, action, entityType, entityId, expectedPermission);
        
        auditLog.EntityDescription.Should().Contain(expectedEntityDescription);

        if (expectedChanges != null && expectedChanges.Length > 0)
        {
            VerifyAuditLogChanges(auditLog, expectedChanges);
        }

        if (expectedIpAddress != null)
        {
            auditLog.IpAddress.Should().Be(expectedIpAddress);
        }

        if (expectedUserAgent != null)
        {
            auditLog.UserAgent.Should().Be(expectedUserAgent);
        }

        return auditLog;
    }

    /// <summary>
    /// Clears all audit logs from the test database
    /// </summary>
    public static async Task ClearAuditLogsAsync(GenoDbContext context)
    {
        var auditLogs = await context.AuditLogs.ToListAsync();
        context.AuditLogs.RemoveRange(auditLogs);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets audit logs grouped by action type
    /// </summary>
    public static async Task<Dictionary<string, List<AuditLog>>> GetAuditLogsByActionAsync(GenoDbContext context)
    {
        var auditLogs = await context.AuditLogs.ToListAsync();
        return auditLogs.GroupBy(a => a.Action).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Gets audit logs grouped by entity type
    /// </summary>
    public static async Task<Dictionary<string, List<AuditLog>>> GetAuditLogsByEntityTypeAsync(GenoDbContext context)
    {
        var auditLogs = await context.AuditLogs.ToListAsync();
        return auditLogs.GroupBy(a => a.EntityType).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Verifies audit log statistics match expected values
    /// </summary>
    public static async Task VerifyAuditLogStatisticsAsync(
        GenoDbContext context,
        int expectedTotalCount,
        int? expectedCreateCount = null,
        int? expectedUpdateCount = null,
        int? expectedDeleteCount = null)
    {
        var totalCount = await context.AuditLogs.CountAsync();
        totalCount.Should().Be(expectedTotalCount);

        if (expectedCreateCount.HasValue)
        {
            var createCount = await context.AuditLogs.CountAsync(a => a.Action == "Create");
            createCount.Should().Be(expectedCreateCount.Value);
        }

        if (expectedUpdateCount.HasValue)
        {
            var updateCount = await context.AuditLogs.CountAsync(a => a.Action == "Update");
            updateCount.Should().Be(expectedUpdateCount.Value);
        }

        if (expectedDeleteCount.HasValue)
        {
            var deleteCount = await context.AuditLogs.CountAsync(a => a.Action == "Delete");
            deleteCount.Should().Be(expectedDeleteCount.Value);
        }
    }
}