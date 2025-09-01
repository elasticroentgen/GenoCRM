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

public class MemberServiceCrudTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<MemberService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly MemberService _memberService;

    public MemberServiceCrudTests()
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

        _memberService = new MemberService(
            _context, 
            _mockLogger.Object, 
            _configuration,
            _mockShareService.Object, 
            _mockFiscalYearService.Object, 
            _mockAuditService.Object,
            _mockHttpContextAccessor.Object);
    }

    #region Create Tests

    [Fact]
    public async Task CreateMemberAsync_WithValidMember_ShouldSucceed()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1234567890",
            Street = "123 Main St",
            PostalCode = "12345",
            City = "Anytown",
            Country = "USA",
            BirthDate = new DateTime(1990, 1, 1),
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act
        var result = await _memberService.CreateMemberAsync(member, 1);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.MemberNumber.Should().NotBeNullOrEmpty();
        result.MemberNumber.Should().StartWith("M");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify member was saved to database
        var savedMember = await _context.Members.FindAsync(result.Id);
        savedMember.Should().NotBeNull();
        savedMember!.FirstName.Should().Be("John");
        savedMember.LastName.Should().Be("Doe");
        savedMember.Status.Should().Be(MemberStatus.Active);

        // Verify initial share was created
        var shares = await _context.CooperativeShares.Where(s => s.MemberId == result.Id).ToListAsync();
        shares.Should().HaveCount(1);
        shares[0].Quantity.Should().Be(1);
        shares[0].Value.Should().Be(250.00m);
        shares[0].Status.Should().Be(ShareStatus.Active);
    }

    [Fact]
    public async Task CreateMemberAsync_WithMultipleInitialShares_ShouldCreateCorrectQuantity()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "+1234567890",
            Street = "456 Oak Ave",
            PostalCode = "67890",
            City = "Otherville",
            Country = "USA",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act
        var result = await _memberService.CreateMemberAsync(member, 5);

        // Assert
        result.Should().NotBeNull();
        
        // Verify initial shares were created with correct quantity
        var shares = await _context.CooperativeShares.Where(s => s.MemberId == result.Id).ToListAsync();
        shares.Should().HaveCount(1);
        shares[0].Quantity.Should().Be(5);
        shares[0].Value.Should().Be(250.00m);
        shares[0].NominalValue.Should().Be(250.00m);
    }

    [Fact]
    public async Task CreateMemberAsync_WithExcessiveShares_ShouldThrowException()
    {
        // Arrange
        var member = new Member
        {
            FirstName = "Bob",
            LastName = "Johnson",
            Email = "bob.johnson@example.com",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _memberService.CreateMemberAsync(member, 150)); // Exceeds max of 100

        exception.Message.Should().Contain("exceeds maximum allowed shares");
    }

    [Fact]
    public async Task CreateMemberAsync_ShouldAutoGenerateSequentialMemberNumbers()
    {
        // Arrange
        var member1 = new Member { FirstName = "Member", LastName = "One", Email = "one@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active };
        var member2 = new Member { FirstName = "Member", LastName = "Two", Email = "two@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active };
        var member3 = new Member { FirstName = "Member", LastName = "Three", Email = "three@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active };

        // Act
        var result1 = await _memberService.CreateMemberAsync(member1);
        var result2 = await _memberService.CreateMemberAsync(member2);
        var result3 = await _memberService.CreateMemberAsync(member3);

        // Assert
        result1.MemberNumber.Should().Be("M001");
        result2.MemberNumber.Should().Be("M002");
        result3.MemberNumber.Should().Be("M003");
    }

    #endregion

    #region Read Tests

    [Fact]
    public async Task GetAllMembersAsync_ShouldReturnAllMembers()
    {
        // Arrange
        var members = new List<Member>
        {
            new Member { MemberNumber = "M001", FirstName = "John", LastName = "Doe", Email = "john@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M002", FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M003", FirstName = "Bob", LastName = "Johnson", Email = "bob@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Inactive }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetAllMembersAsync();

        // Assert
        var memberList = result.ToList();
        memberList.Should().HaveCount(3);
        memberList.Should().BeInAscendingOrder(m => m.MemberNumber);
    }

    [Fact]
    public async Task GetMemberByIdAsync_WithExistingId_ShouldReturnMember()
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
        var result = await _memberService.GetMemberByIdAsync(member.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(member.Id);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task GetMemberByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _memberService.GetMemberByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberByNumberAsync_WithExistingNumber_ShouldReturnMember()
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
        var result = await _memberService.GetMemberByNumberAsync("M001");

        // Assert
        result.Should().NotBeNull();
        result!.MemberNumber.Should().Be("M001");
        result.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetMemberByNumberAsync_WithNonExistentNumber_ShouldReturnNull()
    {
        // Act
        var result = await _memberService.GetMemberByNumberAsync("M999");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchMembersAsync_ShouldFindMembersByMultipleFields()
    {
        // Arrange
        var members = new List<Member>
        {
            new Member { MemberNumber = "M001", FirstName = "John", LastName = "Doe", Email = "john.doe@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M002", FirstName = "Jane", LastName = "Smith", Email = "jane.smith@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M003", FirstName = "Bob", LastName = "Miller", Email = "bob.miller@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act & Assert
        var firstNameResults = await _memberService.SearchMembersAsync("John");
        firstNameResults.Should().HaveCount(1);
        firstNameResults.First().FirstName.Should().Be("John");

        var lastNameResults = await _memberService.SearchMembersAsync("Smith");
        lastNameResults.Should().HaveCount(1);
        lastNameResults.First().LastName.Should().Be("Smith");

        var memberNumberResults = await _memberService.SearchMembersAsync("M002");
        memberNumberResults.Should().HaveCount(1);
        memberNumberResults.First().MemberNumber.Should().Be("M002");

        var emailResults = await _memberService.SearchMembersAsync("bob.miller");
        emailResults.Should().HaveCount(1);
        emailResults.First().Email.Should().Be("bob.miller@test.com");
    }

    [Fact]
    public async Task GetMembersByStatusAsync_ShouldReturnOnlyMembersWithSpecifiedStatus()
    {
        // Arrange
        var members = new List<Member>
        {
            new Member { MemberNumber = "M001", FirstName = "Active1", LastName = "Member", Email = "active1@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M002", FirstName = "Active2", LastName = "Member", Email = "active2@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M003", FirstName = "Inactive", LastName = "Member", Email = "inactive@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Inactive }
        };

        _context.Members.AddRange(members);
        await _context.SaveChangesAsync();

        // Act
        var activeMembers = await _memberService.GetMembersByStatusAsync(MemberStatus.Active);
        var inactiveMembers = await _memberService.GetMembersByStatusAsync(MemberStatus.Inactive);

        // Assert
        activeMembers.Should().HaveCount(2);
        activeMembers.Should().OnlyContain(m => m.Status == MemberStatus.Active);
        
        inactiveMembers.Should().HaveCount(1);
        inactiveMembers.Should().OnlyContain(m => m.Status == MemberStatus.Inactive);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateMemberAsync_WithValidChanges_ShouldUpdateMember()
    {
        // Arrange
        var originalMember = new Member
        {
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@old.com",
            Phone = "111-111-1111",
            Street = "Old Street",
            PostalCode = "11111",
            City = "Old City",
            Country = "Old Country",
            BirthDate = new DateTime(1990, 1, 1),
            JoinDate = DateTime.UtcNow.AddYears(-1),
            Status = MemberStatus.Active,
            Notes = "Old notes"
        };

        _context.Members.Add(originalMember);
        await _context.SaveChangesAsync();

        var updateMember = new Member
        {
            Id = originalMember.Id,
            MemberNumber = "M999", // This should be ignored
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@new.com",
            Phone = "222-222-2222",
            Street = "New Street",
            PostalCode = "22222",
            City = "New City",
            Country = "New Country",
            BirthDate = new DateTime(1985, 6, 15),
            Status = MemberStatus.Inactive,
            LeaveDate = DateTime.UtcNow,
            Notes = "Updated notes"
        };

        // Act
        var result = await _memberService.UpdateMemberAsync(updateMember);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(originalMember.Id);
        result.MemberNumber.Should().Be("M001"); // Should remain unchanged
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane.smith@new.com");
        result.Phone.Should().Be("222-222-2222");
        result.Street.Should().Be("New Street");
        result.PostalCode.Should().Be("22222");
        result.City.Should().Be("New City");
        result.Country.Should().Be("New Country");
        result.BirthDate.Should().Be(new DateTime(1985, 6, 15));
        result.Status.Should().Be(MemberStatus.Inactive);
        result.Notes.Should().Be("Updated notes");
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify changes were persisted
        var updatedMember = await _context.Members.FindAsync(originalMember.Id);
        updatedMember!.FirstName.Should().Be("Jane");
        updatedMember.MemberNumber.Should().Be("M001"); // Should not have changed
    }

    [Fact]
    public async Task UpdateMemberAsync_WithNonExistentId_ShouldThrowException()
    {
        // Arrange
        var nonExistentMember = new Member
        {
            Id = 999,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _memberService.UpdateMemberAsync(nonExistentMember));

        exception.Message.Should().Contain("not found");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteMemberAsync_WithExistingMember_ShouldSoftDelete()
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
        var result = await _memberService.DeleteMemberAsync(member.Id);

        // Assert
        result.Should().BeTrue();

        // Verify soft delete (status changed, not removed)
        var deletedMember = await _context.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == member.Id);
        deletedMember.Should().NotBeNull();
        deletedMember!.Status.Should().Be(MemberStatus.Terminated);
        deletedMember.LeaveDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        deletedMember.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DeleteMemberAsync_WithNonExistentMember_ShouldReturnFalse()
    {
        // Act
        var result = await _memberService.DeleteMemberAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Utility Tests

    [Fact]
    public async Task MemberNumberExistsAsync_WithExistingNumber_ShouldReturnTrue()
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
        var result = await _memberService.MemberNumberExistsAsync("M001");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MemberNumberExistsAsync_WithNonExistentNumber_ShouldReturnFalse()
    {
        // Act
        var result = await _memberService.MemberNumberExistsAsync("M999");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateNextMemberNumberAsync_WithExistingMembers_ShouldGenerateCorrectNextNumber()
    {
        // Arrange
        var existingMembers = new List<Member>
        {
            new Member { MemberNumber = "M001", FirstName = "One", LastName = "Member", Email = "one@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M003", FirstName = "Three", LastName = "Member", Email = "three@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Active },
            new Member { MemberNumber = "M005", FirstName = "Five", LastName = "Member", Email = "five@test.com", JoinDate = DateTime.UtcNow, Status = MemberStatus.Terminated }
        };

        _context.Members.AddRange(existingMembers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GenerateNextMemberNumberAsync();

        // Assert
        result.Should().Be("M006"); // Should find the highest number (M005) and increment
    }

    [Fact]
    public async Task GenerateNextMemberNumberAsync_WithNoExistingMembers_ShouldReturnM001()
    {
        // Act
        var result = await _memberService.GenerateNextMemberNumberAsync();

        // Assert
        result.Should().Be("M001");
    }

    [Fact]
    public async Task GetCurrentShareDenominationAsync_ShouldReturnConfiguredValue()
    {
        // Act
        var result = await _memberService.GetCurrentShareDenominationAsync();

        // Assert
        result.Should().Be(250.00m);
    }

    [Fact]
    public async Task GetMaxSharesPerMemberAsync_ShouldReturnConfiguredValue()
    {
        // Act
        var result = await _memberService.GetMaxSharesPerMemberAsync();

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public async Task GetMemberTotalShareValueAsync_ShouldCalculateCorrectTotal()
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

        var shares = new List<CooperativeShare>
        {
            new CooperativeShare { MemberId = member.Id, Quantity = 2, Value = 250.00m, Status = ShareStatus.Active },
            new CooperativeShare { MemberId = member.Id, Quantity = 3, Value = 250.00m, Status = ShareStatus.Active },
            new CooperativeShare { MemberId = member.Id, Quantity = 1, Value = 250.00m, Status = ShareStatus.Cancelled } // Should be excluded
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetMemberTotalShareValueAsync(member.Id);

        // Assert
        result.Should().Be(1250.00m); // (2 * 250) + (3 * 250) = 1250
    }

    [Fact]
    public async Task GetMemberTotalPaymentsAsync_ShouldCalculateCorrectTotal()
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

        var payments = new List<Payment>
        {
            new Payment { MemberId = member.Id, Amount = 250.00m, Status = PaymentStatus.Completed },
            new Payment { MemberId = member.Id, Amount = 500.00m, Status = PaymentStatus.Completed },
            new Payment { MemberId = member.Id, Amount = 100.00m, Status = PaymentStatus.Pending } // Should be excluded
        };

        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _memberService.GetMemberTotalPaymentsAsync(member.Id);

        // Assert
        result.Should().Be(750.00m); // 250 + 500 = 750
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}