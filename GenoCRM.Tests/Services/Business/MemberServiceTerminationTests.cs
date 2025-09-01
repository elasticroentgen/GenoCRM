using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class MemberServiceTerminationTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<MemberService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly MemberService _memberService;

    public MemberServiceTerminationTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<MemberService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockShareService = new Mock<IShareService>();
        _mockFiscalYearService = new Mock<IFiscalYearService>();
        _mockAuditService = new Mock<IAuditService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup HTTP context with mock user
        var mockHttpContext = new Mock<HttpContext>();
        var mockUser = new Mock<ClaimsPrincipal>();
        mockUser.Setup(x => x.Identity!.Name).Returns("test-user");
        mockHttpContext.Setup(x => x.User).Returns(mockUser.Object);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        _memberService = new MemberService(_context, _mockLogger.Object, _mockConfiguration.Object, 
            _mockShareService.Object, _mockFiscalYearService.Object, _mockAuditService.Object, _mockHttpContextAccessor.Object);

        // Setup fiscal year service defaults
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns((int year) => new DateTime(year, 12, 31));
        _mockFiscalYearService.Setup(x => x.GetFiscalYearForDate(It.IsAny<DateTime>()))
            .Returns((DateTime date) => date.Year);
    }

    [Fact]
    public async Task SubmitTerminationNoticeAsync_WithActiveMembe_ShouldSucceed()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1)
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.SubmitTerminationNoticeAsync(member.Id);

        // Assert
        Assert.True(result);
        
        var updatedMember = await _context.Members.FindAsync(member.Id);
        Assert.NotNull(updatedMember);
        Assert.NotNull(updatedMember.TerminationNoticeDate);
        Assert.True(updatedMember.TerminationNoticeDate.Value <= DateTime.UtcNow);
    }

    [Fact]
    public async Task SubmitTerminationNoticeAsync_WithInactiveMember_ShouldFail()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Inactive,
            JoinDate = DateTime.UtcNow.AddYears(-1)
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.SubmitTerminationNoticeAsync(member.Id);

        // Assert
        Assert.False(result);
        
        var updatedMember = await _context.Members.FindAsync(member.Id);
        Assert.NotNull(updatedMember);
        Assert.Null(updatedMember.TerminationNoticeDate);
    }

    [Fact]
    public async Task SubmitTerminationNoticeAsync_WithExistingNotice_ShouldFail()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1),
            TerminationNoticeDate = DateTime.UtcNow.AddDays(-30)
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.SubmitTerminationNoticeAsync(member.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetEarliestTerminationDateAsync_WithNoticeDate_ShouldReturnCorrectDate()
    {
        // Arrange
        var noticeDate = new DateTime(2023, 6, 15);
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1),
            TerminationNoticeDate = noticeDate
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Setup fiscal year service to return specific dates
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(2025))
            .Returns(new DateTime(2025, 12, 31));

        // Act
        var result = await _memberService.GetEarliestTerminationDateAsync(member.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2025, 12, 31), result.Value);
    }

    [Fact]
    public async Task GetEarliestTerminationDateAsync_WithoutNoticeDate_ShouldReturnNull()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1)
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetEarliestTerminationDateAsync(member.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CanOffboardMemberAsync_WithValidNoticeAndTimeElapsed_ShouldReturnTrue()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddYears(-3);
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-4),
            TerminationNoticeDate = pastDate
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Setup fiscal year service to return date in the past
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _memberService.CanOffboardMemberAsync(member.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanOffboardMemberAsync_WithoutNoticeDate_ShouldReturnFalse()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1)
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.CanOffboardMemberAsync(member.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanOffboardMemberAsync_WithRecentNotice_ShouldReturnFalse()
    {
        // Arrange
        var recentDate = DateTime.UtcNow.AddDays(-30);
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1),
            TerminationNoticeDate = recentDate
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Setup fiscal year service to return future date
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns(DateTime.UtcNow.AddYears(1));

        // Act
        var result = await _memberService.CanOffboardMemberAsync(member.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMembersReadyForOffboardingAsync_ShouldReturnEligibleMembers()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddYears(-3);
        var futureDate = DateTime.UtcNow.AddDays(-30);

        var members = new List<Member>
        {
            new Member
            {
                Id = 1,
                MemberNumber = "M001",
                FirstName = "John",
                LastName = "Doe",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow.AddYears(-4),
                TerminationNoticeDate = pastDate
            },
            new Member
            {
                Id = 2,
                MemberNumber = "M002",
                FirstName = "Jane",
                LastName = "Smith",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow.AddYears(-2),
                TerminationNoticeDate = futureDate
            },
            new Member
            {
                Id = 3,
                MemberNumber = "M003",
                FirstName = "Bob",
                LastName = "Johnson",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow.AddYears(-1)
            }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Setup fiscal year service - member 1 should be eligible, member 2 should not
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns((int year) => year <= DateTime.UtcNow.Year - 1 ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddYears(1));

        // Act
        var result = await _memberService.GetMembersReadyForOffboardingAsync();

        // Assert
        var readyMembers = result.ToList();
        Assert.Single(readyMembers);
        Assert.Equal(1, readyMembers[0].Id);
    }

    [Theory]
    [InlineData(2023, 6, 15, 2025, 12, 31)] // Notice in June 2023, should terminate end of 2025
    [InlineData(2023, 1, 1, 2025, 12, 31)]  // Notice in January 2023, should terminate end of 2025
    [InlineData(2023, 12, 31, 2025, 12, 31)] // Notice in December 2023, should terminate end of 2025
    public async Task GetEarliestTerminationDateAsync_WithVariousNoticeDates_ShouldReturnCorrectFiscalYearEnd(
        int noticeYear, int noticeMonth, int noticeDay, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var noticeDate = new DateTime(noticeYear, noticeMonth, noticeDay);
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-1),
            TerminationNoticeDate = noticeDate
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Setup fiscal year service
        _mockFiscalYearService.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>()))
            .Returns((int year) => new DateTime(year, 12, 31));
        _mockFiscalYearService.Setup(x => x.GetFiscalYearForDate(It.IsAny<DateTime>()))
            .Returns((DateTime date) => date.Year);

        // Act
        var result = await _memberService.GetEarliestTerminationDateAsync(member.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), result.Value);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}