using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class AuditServiceRobustnessTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly AuditService _service;

    public AuditServiceRobustnessTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _service = new AuditService(_context, new Mock<ILogger<AuditService>>().Object);
    }

    [Fact]
    public async Task LogActionAsync_TruncatesOverlongDescriptionsToTheColumnLimit()
    {
        var description = new string('x', 300);

        await _service.LogActionAsync("u", AuditAction.Create, "Member", "1", description, Permissions.CreateMembers);

        var entry = await _context.AuditLogs.SingleAsync();
        Assert.Equal(200, entry.EntityDescription.Length);
    }

    /// <summary>A context whose first SaveChangesAsync fails, like a constraint violation on the audit insert.</summary>
    private sealed class FailOnceContext : GenoDbContext
    {
        private bool _failed;
        public FailOnceContext(DbContextOptions<GenoDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw new DbUpdateException("simulated constraint violation");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task LogActionAsync_FailedEntryDoesNotPoisonLaterSaves()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new FailOnceContext(options);
        var service = new AuditService(context, new Mock<ILogger<AuditService>>().Object);

        await service.LogActionAsync("u", AuditAction.Update, "Member", "1", "desc", Permissions.EditMembers);

        Assert.DoesNotContain(context.ChangeTracker.Entries<AuditLog>(), e => e.State == EntityState.Added);

        // An ordinary save afterwards must succeed and must not retry the failed audit row
        context.Members.Add(new Member { MemberNumber = "M001", FirstName = "A", LastName = "B", Email = "a@b.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow });
        await context.SaveChangesAsync();
        Assert.Equal(1, await context.Members.CountAsync());
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task GetAuditLogsAsync_ToDateIncludesEntriesOnThatDay()
    {
        var today = DateTime.UtcNow.Date;
        _context.AuditLogs.AddRange(
            new AuditLog { UserName = "u", Action = "Create", EntityType = "Member", EntityDescription = "early", Timestamp = today.AddDays(-1).AddHours(10) },
            new AuditLog { UserName = "u", Action = "Create", EntityType = "Member", EntityDescription = "today", Timestamp = today.AddHours(15) },
            new AuditLog { UserName = "u", Action = "Create", EntityType = "Member", EntityDescription = "tomorrow", Timestamp = today.AddDays(1).AddHours(1) });
        await _context.SaveChangesAsync();

        var logs = (await _service.GetAuditLogsAsync(fromDate: today, toDate: today)).Select(l => l.EntityDescription).ToList();
        var count = await _service.GetAuditLogCountAsync(fromDate: today, toDate: today);

        Assert.Equal(new[] { "today" }, logs);
        Assert.Equal(1, count);
    }

    public void Dispose() => _context.Dispose();
}
