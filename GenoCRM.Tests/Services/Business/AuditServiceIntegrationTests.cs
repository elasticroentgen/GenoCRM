using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GenoCRM.Tests.Services.Business;

public class AuditServiceIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GenoDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditService> _logger;

    public AuditServiceIntegrationTests()
    {
        var databaseName = $"AuditTestDb_{Guid.NewGuid()}";
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Add DbContext with in-memory database
        services.AddDbContext<GenoDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: databaseName)
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        
        // Add audit service
        services.AddScoped<IAuditService, AuditService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<GenoDbContext>();
        _auditService = _serviceProvider.GetRequiredService<IAuditService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<AuditService>>();
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task LogActionAsync_ShouldCreateAuditLogEntry()
    {
        // Arrange
        var userName = "testuser";
        var action = AuditAction.Create;
        var entityType = "Member";
        var entityId = "123";
        var entityDescription = "John Doe (M001)";
        var permission = "CreateMembers";
        var changes = new { Name = "John Doe", MemberNumber = "M001" };
        var ipAddress = "192.168.1.1";
        var userAgent = "Test Browser";

        // Act
        await _auditService.LogActionAsync(
            userName, action, entityType, entityId, entityDescription,
            permission, changes, ipAddress, userAgent);

        // Assert
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.UserName.Should().Be(userName);
        auditLog.Action.Should().Be(action.ToString());
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.EntityDescription.Should().Be(entityDescription);
        auditLog.Permission.Should().Be(permission);
        auditLog.Changes.Should().NotBeNull();
        auditLog.Changes.Should().Contain("John Doe");
        auditLog.IpAddress.Should().Be(ipAddress);
        auditLog.UserAgent.Should().Be(userAgent);
        auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogActionAsync_WithNullChanges_ShouldCreateAuditLogEntry()
    {
        // Arrange
        var userName = "testuser";
        var action = AuditAction.Delete;
        var entityType = "Member";
        var entityId = "456";
        var entityDescription = "Jane Smith (M002)";

        // Act
        await _auditService.LogActionAsync(
            userName, action, entityType, entityId, entityDescription);

        // Assert
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.UserName.Should().Be(userName);
        auditLog.Action.Should().Be(action.ToString());
        auditLog.EntityType.Should().Be(entityType);
        auditLog.EntityId.Should().Be(entityId);
        auditLog.EntityDescription.Should().Be(entityDescription);
        auditLog.Permission.Should().BeNull();
        auditLog.Changes.Should().BeNull();
        auditLog.IpAddress.Should().BeNull();
        auditLog.UserAgent.Should().BeNull();
    }

    [Fact]
    public async Task GetAuditLogsAsync_ShouldReturnPaginatedResults()
    {
        // Arrange
        var testData = new[]
        {
            new { UserName = "user1", Action = AuditAction.Create, EntityType = "Member", EntityId = "1", Description = "Member 1" },
            new { UserName = "user2", Action = AuditAction.Update, EntityType = "Share", EntityId = "2", Description = "Share 2" },
            new { UserName = "user3", Action = AuditAction.Delete, EntityType = "Member", EntityId = "3", Description = "Member 3" },
            new { UserName = "user1", Action = AuditAction.Create, EntityType = "Payment", EntityId = "4", Description = "Payment 4" }
        };

        foreach (var data in testData)
        {
            await _auditService.LogActionAsync(
                data.UserName, data.Action, data.EntityType, 
                data.EntityId, data.Description);
        }

        // Act
        var results = await _auditService.GetAuditLogsAsync(page: 1, pageSize: 2);

        // Assert
        results.Should().HaveCount(2);
        // Results should be ordered by timestamp descending (most recent first)
        var resultList = results.ToList();
        resultList[0].EntityDescription.Should().Be("Payment 4"); // Most recent
        resultList[1].EntityDescription.Should().Be("Member 3");
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithEntityTypeFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _auditService.LogActionAsync("user1", AuditAction.Create, "Member", "1", "Member 1");
        await _auditService.LogActionAsync("user2", AuditAction.Update, "Share", "2", "Share 2");
        await _auditService.LogActionAsync("user3", AuditAction.Create, "Member", "3", "Member 3");

        // Act
        var results = await _auditService.GetAuditLogsAsync(entityType: "Member");

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(log => log.EntityType == "Member");
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithUserNameFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _auditService.LogActionAsync("alice", AuditAction.Create, "Member", "1", "Member 1");
        await _auditService.LogActionAsync("bob", AuditAction.Update, "Share", "2", "Share 2");
        await _auditService.LogActionAsync("alice", AuditAction.Delete, "Payment", "3", "Payment 3");

        // Act
        var results = await _auditService.GetAuditLogsAsync(userName: "alice");

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(log => log.UserName == "alice");
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithDateFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.Date;
        var fromDate = baseDate.AddDays(-1);
        var toDate = baseDate.AddDays(1);

        await _auditService.LogActionAsync("user1", AuditAction.Create, "Member", "1", "Member 1");
        await Task.Delay(10); // Small delay to ensure different timestamps
        await _auditService.LogActionAsync("user2", AuditAction.Update, "Share", "2", "Share 2");

        // Act
        var results = await _auditService.GetAuditLogsAsync(fromDate: fromDate, toDate: toDate);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(log => log.Timestamp >= fromDate && log.Timestamp <= toDate);
    }

    [Fact]
    public async Task GetAuditLogCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await _auditService.LogActionAsync("user1", AuditAction.Create, "Member", "1", "Member 1");
        await _auditService.LogActionAsync("user2", AuditAction.Update, "Share", "2", "Share 2");
        await _auditService.LogActionAsync("user3", AuditAction.Delete, "Payment", "3", "Payment 3");

        // Act
        var totalCount = await _auditService.GetAuditLogCountAsync();
        var memberCount = await _auditService.GetAuditLogCountAsync(entityType: "Member");
        var userCount = await _auditService.GetAuditLogCountAsync(userName: "user1");

        // Assert
        totalCount.Should().Be(3);
        memberCount.Should().Be(1);
        userCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEntityAuditHistoryAsync_ShouldReturnEntitySpecificHistory()
    {
        // Arrange
        var entityType = "Member";
        var entityId = "123";
        var otherEntityId = "456";

        await _auditService.LogActionAsync("user1", AuditAction.Create, entityType, entityId, "Created member");
        await _auditService.LogActionAsync("user2", AuditAction.Update, entityType, entityId, "Updated member");
        await _auditService.LogActionAsync("user3", AuditAction.Create, entityType, otherEntityId, "Other member");
        await _auditService.LogActionAsync("user1", AuditAction.Delete, entityType, entityId, "Deleted member");

        // Act
        var history = await _auditService.GetEntityAuditHistoryAsync(entityType, entityId);

        // Assert
        var historyList = history.ToList();
        historyList.Should().HaveCount(3);
        historyList.Should().OnlyContain(log => log.EntityType == entityType && log.EntityId == entityId);
        
        // Should be ordered by timestamp descending
        historyList[0].Action.Should().Be("Delete"); // Most recent
        historyList[1].Action.Should().Be("Update");
        historyList[2].Action.Should().Be("Create"); // Oldest
    }

    [Fact]
    public async Task GetAuditedEntityTypesAsync_ShouldReturnDistinctEntityTypes()
    {
        // Arrange
        await _auditService.LogActionAsync("user1", AuditAction.Create, "Member", "1", "Member 1");
        await _auditService.LogActionAsync("user2", AuditAction.Update, "Share", "2", "Share 2");
        await _auditService.LogActionAsync("user3", AuditAction.Create, "Member", "3", "Member 3");
        await _auditService.LogActionAsync("user4", AuditAction.Delete, "Payment", "4", "Payment 4");

        // Act
        var entityTypes = await _auditService.GetAuditedEntityTypesAsync();

        // Assert
        var typesList = entityTypes.ToList();
        typesList.Should().HaveCount(3);
        typesList.Should().Contain("Member");
        typesList.Should().Contain("Share");
        typesList.Should().Contain("Payment");
        typesList.Should().BeInAscendingOrder(); // Should be ordered alphabetically
    }

    [Fact]
    public async Task GetAuditedUsersAsync_ShouldReturnDistinctUsers()
    {
        // Arrange
        await _auditService.LogActionAsync("alice", AuditAction.Create, "Member", "1", "Member 1");
        await _auditService.LogActionAsync("bob", AuditAction.Update, "Share", "2", "Share 2");
        await _auditService.LogActionAsync("alice", AuditAction.Delete, "Payment", "3", "Payment 3");
        await _auditService.LogActionAsync("charlie", AuditAction.Create, "Dividend", "4", "Dividend 4");

        // Act
        var users = await _auditService.GetAuditedUsersAsync();

        // Assert
        var usersList = users.ToList();
        usersList.Should().HaveCount(3);
        usersList.Should().Contain("alice");
        usersList.Should().Contain("bob");
        usersList.Should().Contain("charlie");
        usersList.Should().BeInAscendingOrder(); // Should be ordered alphabetically
    }

    [Theory]
    [InlineData(AuditAction.Create)]
    [InlineData(AuditAction.Update)]
    [InlineData(AuditAction.Delete)]
    [InlineData(AuditAction.Transfer)]
    [InlineData(AuditAction.Approve)]
    [InlineData(AuditAction.Cancel)]
    [InlineData(AuditAction.Pay)]
    [InlineData(AuditAction.Suspend)]
    [InlineData(AuditAction.Reactivate)]
    public async Task LogActionAsync_WithAllAuditActions_ShouldCreateAuditLogEntries(AuditAction action)
    {
        // Arrange
        var userName = "testuser";
        var entityType = "TestEntity";
        var entityId = "test-id";
        var entityDescription = $"Test {action}";

        // Act
        await _auditService.LogActionAsync(userName, action, entityType, entityId, entityDescription);

        // Assert
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be(action.ToString());
    }

    [Fact]
    public async Task LogActionAsync_WithComplexChangesObject_ShouldSerializeCorrectly()
    {
        // Arrange
        var changes = new
        {
            Name = "John Doe",
            MemberNumber = "M001",
            ContactInfo = new
            {
                Email = "john@example.com",
                Phone = "+1234567890"
            },
            Shares = new[]
            {
                new { CertificateNumber = "C001", Quantity = 10 },
                new { CertificateNumber = "C002", Quantity = 5 }
            }
        };

        // Act
        await _auditService.LogActionAsync(
            "testuser", AuditAction.Update, "Member", "123", "John Doe", 
            changes: changes);

        // Assert
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Changes.Should().NotBeNull();
        auditLog.Changes.Should().Contain("John Doe");
        auditLog.Changes.Should().Contain("john@example.com");
        auditLog.Changes.Should().Contain("C001");
        auditLog.Changes.Should().Contain("\"Quantity\": 10");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.GetService<IServiceScope>()?.Dispose();
    }
}