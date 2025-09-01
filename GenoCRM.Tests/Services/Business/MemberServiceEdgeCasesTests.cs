using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace GenoCRM.Tests.Services.Business;

public class MemberServiceEdgeCasesTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<MemberService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly MemberService _memberService;

    public MemberServiceEdgeCasesTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<MemberService>>();
        
        // Create a real configuration with in-memory values
        var inMemorySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"},
            {"CooperativeSettings:MaxSharesPerMember", "100"}
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        
        _mockShareService = new Mock<IShareService>();
        _mockFiscalYearService = new Mock<IFiscalYearService>();
        _mockAuditService = new Mock<IAuditService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        SetupDefaultMocks();

        _memberService = new MemberService(
            _context, 
            _mockLogger.Object, 
            _configuration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);
    }

    private void SetupDefaultMocks()
    {
        // Configuration is already set up with real in-memory configuration

        // Setup share service defaults
        _mockShareService.Setup(x => x.GenerateNextCertificateNumberAsync())
            .ReturnsAsync("CERT001");

        // Setup fiscal year service defaults
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns((int year) => new DateTime(year, 12, 31));
        _mockFiscalYearService.Setup(x => x.GetFiscalYearForDate(It.IsAny<DateTime>()))
            .Returns((DateTime date) => date.Year);

        // Setup HTTP context with mock user
        var mockHttpContext = new Mock<HttpContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(x => x.Identity!.Name).Returns("test-user");
        mockHttpContext.Setup(x => x.User).Returns(mockUser.Object);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
    }

    #region Configuration Edge Cases

    [Fact]
    public async Task GetCurrentShareDenominationAsync_WithInvalidConfig_ShouldReturnDefault()
    {
        // Arrange - Create a service with invalid configuration
        var invalidSettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "0"}, // Invalid configuration
            {"CooperativeSettings:MaxSharesPerMember", "100"}
        };
        
        var invalidConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(invalidSettings!)
            .Build();
            
        var memberServiceWithInvalidConfig = new MemberService(
            _context, 
            _mockLogger.Object, 
            invalidConfiguration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);

        // Act
        var result = await memberServiceWithInvalidConfig.GetCurrentShareDenominationAsync();

        // Assert
        result.Should().Be(250.00m); // Should return default value
    }

    [Fact]
    public async Task GetCurrentShareDenominationAsync_WithMissingConfig_ShouldReturnDefault()
    {
        // Arrange - Create a service with missing configuration
        var emptySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:MaxSharesPerMember", "100"}
            // Missing ShareDenomination setting
        };
        
        var emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(emptySettings!)
            .Build();
            
        var memberServiceWithMissingConfig = new MemberService(
            _context, 
            _mockLogger.Object, 
            emptyConfiguration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);

        // Act
        var result = await memberServiceWithMissingConfig.GetCurrentShareDenominationAsync();

        // Assert
        result.Should().Be(250.00m); // Should return default value
    }

    [Fact]
    public async Task GetMaxSharesPerMemberAsync_WithInvalidConfig_ShouldReturnDefault()
    {
        // Arrange - Create a service with invalid configuration
        var invalidSettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"},
            {"CooperativeSettings:MaxSharesPerMember", "-1"} // Invalid configuration
        };
        
        var invalidConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(invalidSettings!)
            .Build();
            
        var memberServiceWithInvalidConfig = new MemberService(
            _context, 
            _mockLogger.Object, 
            invalidConfiguration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);

        // Act
        var result = await memberServiceWithInvalidConfig.GetMaxSharesPerMemberAsync();

        // Assert
        result.Should().Be(100); // Should return default value
    }

    [Fact]
    public async Task GetMaxSharesPerMemberAsync_WithMissingConfig_ShouldReturnDefault()
    {
        // Arrange - Create a service with missing configuration
        var emptySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"}
            // Missing MaxSharesPerMember setting
        };
        
        var emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(emptySettings!)
            .Build();
            
        var memberServiceWithMissingConfig = new MemberService(
            _context, 
            _mockLogger.Object, 
            emptyConfiguration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);

        // Act
        var result = await memberServiceWithMissingConfig.GetMaxSharesPerMemberAsync();

        // Assert
        result.Should().Be(100); // Should return default value
    }

    #endregion

    #region Database Transaction Edge Cases

    [Fact(Skip = "In-memory database doesn't support realistic transaction rollback testing")]
    public async Task CreateMemberAsync_WithDatabaseError_ShouldRollbackTransaction()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Setup share service to fail after member is created
        _mockShareService.Setup(x => x.GenerateNextCertificateNumberAsync())
            .ThrowsAsync(new Exception("Certificate generation failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _memberService.CreateMemberAsync(member, 1));

        exception.Message.Should().Be("Certificate generation failed");

        // Verify transaction was rolled back - no member should exist
        var memberCount = await _context.Members.CountAsync();
        memberCount.Should().Be(0);

        var shareCount = await _context.CooperativeShares.CountAsync();
        shareCount.Should().Be(0);
    }

    #endregion

    #region Search Edge Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task SearchMembersAsync_WithEmptyOrWhitespaceSearch_ShouldHandleGracefully(string searchTerm)
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.SearchMembersAsync(searchTerm);

        // Assert
        // Should handle empty search gracefully (may return all or none, depending on implementation)
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchMembersAsync_WithSpecialCharacters_ShouldHandleGracefully()
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "O'Connor", // Contains apostrophe
            Email = "john.oconnor@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.SearchMembersAsync("O'Connor");

        // Assert
        result.Should().HaveCount(1);
        result.First().LastName.Should().Be("O'Connor");
    }

    [Fact]
    public async Task SearchMembersAsync_CaseInsensitive_ShouldFindMatches()
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var upperResult = await _memberService.SearchMembersAsync("JOHN");
        var lowerResult = await _memberService.SearchMembersAsync("john");
        var mixedResult = await _memberService.SearchMembersAsync("JoHn");

        // Assert
        upperResult.Should().HaveCount(1);
        lowerResult.Should().HaveCount(1);
        mixedResult.Should().HaveCount(1);
        upperResult.First().FirstName.Should().Be("John");
    }

    #endregion

    #region Member Number Generation Edge Cases

    [Fact]
    public async Task GenerateNextMemberNumberAsync_WithNonSequentialNumbers_ShouldFindHighest()
    {
        // Arrange - Create members with non-sequential numbers
        var members = new List<Member>
        {
            new Member { MemberNumber = "M007", FirstName = "Seven", LastName = "Member", Email = "seven@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M003", FirstName = "Three", LastName = "Member", Email = "three@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M015", FirstName = "Fifteen", LastName = "Member", Email = "fifteen@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M001", FirstName = "One", LastName = "Member", Email = "one@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GenerateNextMemberNumberAsync();

        // Assert
        result.Should().Be("M016"); // Should find M015 as highest and increment
    }

    [Fact]
    public async Task GenerateNextMemberNumberAsync_WithInvalidMemberNumbers_ShouldIgnoreInvalid()
    {
        // Arrange - Mix of valid and invalid member numbers
        var members = new List<Member>
        {
            new Member { MemberNumber = "M005", FirstName = "Valid", LastName = "Member", Email = "valid@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "INVALID", FirstName = "Invalid", LastName = "Member", Email = "invalid@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "ABC123", FirstName = "Also Invalid", LastName = "Member", Email = "alsoinvalid@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GenerateNextMemberNumberAsync();

        // Assert
        result.Should().Be("M006"); // Should only consider valid M### format
    }

    [Fact]
    public async Task GenerateNextMemberNumberAsync_WithTerminatedMembers_ShouldIncludeInCount()
    {
        // Arrange - Include terminated members in the count
        var members = new List<Member>
        {
            new Member { MemberNumber = "M001", FirstName = "Active", LastName = "Member", Email = "active@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M002", FirstName = "Terminated", LastName = "Member", Email = "terminated@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Terminated }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GenerateNextMemberNumberAsync();

        // Assert
        result.Should().Be("M003"); // Should consider terminated members too
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public async Task CreateMemberAsync_WithZeroInitialShares_ShouldSucceed()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act
        var result = await _memberService.CreateMemberAsync(member, 0);

        // Assert
        result.Should().NotBeNull();
        
        // Should create a share with quantity 0
        var shares = await _context.CooperativeShares.Where(s => s.MemberId == result.Id).ToListAsync();
        shares.Should().HaveCount(1);
        shares[0].Quantity.Should().Be(0);
    }

    [Fact]
    public async Task CreateMemberAsync_WithMaxAllowedShares_ShouldSucceed()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act
        var result = await _memberService.CreateMemberAsync(member, 100); // Max allowed

        // Assert
        result.Should().NotBeNull();
        
        var shares = await _context.CooperativeShares.Where(s => s.MemberId == result.Id).ToListAsync();
        shares.Should().HaveCount(1);
        shares[0].Quantity.Should().Be(100);
    }

    [Fact]
    public async Task CreateMemberAsync_WithExactlyOneOverMax_ShouldThrowException()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _memberService.CreateMemberAsync(member, 101)); // One over max

        exception.Message.Should().Contain("exceeds maximum allowed shares");
    }

    #endregion

    #region Data Integrity Tests

    [Fact]
    public async Task UpdateMemberAsync_ShouldNotChangeMemberNumber()
    {
        // Arrange
        var originalMember = new Member
        {
            MemberNumber = "M001",
            FirstName = "Original",
            LastName = "Name",
            Email = "original@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(originalMember);
        await _context.SaveChangesAsync();

        var updateMember = new Member
        {
            Id = originalMember.Id,
            MemberNumber = "M999", // Attempt to change member number
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@test.com"
        };

        // Act
        var result = await _memberService.UpdateMemberAsync(updateMember);

        // Assert
        result.MemberNumber.Should().Be("M001"); // Should not have changed
        result.FirstName.Should().Be("Updated"); // Other fields should update
    }

    [Fact]
    public async Task DeleteMemberAsync_ShouldPreserveAuditTrail()
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        var originalId = member.Id;
        var originalMemberNumber = member.MemberNumber;

        // Act
        await _memberService.DeleteMemberAsync(member.Id);

        // Assert - Member should still exist for audit purposes
        var softDeletedMember = await _context.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == originalId);
        softDeletedMember.Should().NotBeNull();
        softDeletedMember!.Id.Should().Be(originalId);
        softDeletedMember.MemberNumber.Should().Be(originalMemberNumber);
        softDeletedMember.Status.Should().Be(MemberStatus.Terminated);
    }

    #endregion

    #region Performance and Concurrency Tests

    [Fact]
    public async Task CreateMemberAsync_ConcurrentCreation_ShouldGenerateUniqueMemberNumbers()
    {
        // Arrange
        var tasks = new List<Task<Member>>();
        
        for (int i = 0; i < 5; i++)
        {
            var member = new Member
            {
                FirstName = $"Member",
                LastName = $"{i + 1}",
                Email = $"member{i + 1}@test.com",
                JoinDate = DateTime.UtcNow,
                Status = MemberStatus.Active
            };
            
            tasks.Add(_memberService.CreateMemberAsync(member, 1));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        var memberNumbers = results.Select(r => r.MemberNumber).ToList();
        memberNumbers.Should().OnlyHaveUniqueItems();
        memberNumbers.Should().AllSatisfy(mn => mn.Should().StartWith("M"));
    }

    #endregion

    #region Error Handling Tests

    [Fact(Skip = "In-memory database doesn't support realistic error simulation")]
    public async Task GetAllMembersAsync_WithDatabaseException_ShouldThrowAndLog()
    {
        // Arrange
        await _context.Database.EnsureDeletedAsync(); // Force database error

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _memberService.GetAllMembersAsync());
        
        // Verify logging occurred (check that logger was called with error level)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving all members")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateMemberAsync_WithNegativeInitialShares_ShouldHandleGracefully()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act
        var result = await _memberService.CreateMemberAsync(member, -1);

        // Assert - Should create share with negative quantity (business logic may allow this)
        result.Should().NotBeNull();
        var shares = await _context.CooperativeShares.Where(s => s.MemberId == result.Id).ToListAsync();
        shares.Should().HaveCount(1);
        shares[0].Quantity.Should().Be(-1);
    }

    #endregion

    [Fact]
    public async Task GetMemberTotalShareValueAsync_WithNoShares_ShouldReturnZero()
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetMemberTotalShareValueAsync(member.Id);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public async Task GetMemberTotalPaymentsAsync_WithNoPayments_ShouldReturnZero()
    {
        // Arrange
        var member = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetMemberTotalPaymentsAsync(member.Id);

        // Assert
        result.Should().Be(0m);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}