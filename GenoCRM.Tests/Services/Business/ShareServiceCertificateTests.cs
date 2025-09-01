using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareServiceCertificateTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<ShareService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly ShareService _shareService;

    public ShareServiceCertificateTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<ShareService>>();
        
        // Create a real configuration with in-memory values
        var inMemorySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"},
            {"CooperativeSettings:MaxSharesPerMember", "100"}
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
            
        _shareService = new ShareService(_context, _mockLogger.Object, _configuration);
    }

    [Fact]
    public async Task GenerateNextCertificateNumberAsync_WithNoExistingShares_ShouldReturnCERT001()
    {
        // Act
        var result = await _shareService.GenerateNextCertificateNumberAsync();

        // Assert
        Assert.Equal("CERT001", result);
    }

    [Fact]
    public async Task GenerateNextCertificateNumberAsync_WithExistingShares_ShouldReturnCorrectNextNumber()
    {
        // Arrange - First create a member to reference
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "Test",
            LastName = "Member",
            Email = "test@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-1),
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };
        
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        
        var existingShares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                Id = 1,
                MemberId = 1,
                CertificateNumber = "CERT001",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new CooperativeShare
            {
                Id = 2,
                MemberId = 1,
                CertificateNumber = "CERT005",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new CooperativeShare
            {
                Id = 3,
                MemberId = 1,
                CertificateNumber = "CERT003",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-25),
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                UpdatedAt = DateTime.UtcNow.AddDays(-25)
            }
        };

        _context.CooperativeShares.AddRange(existingShares);
        await _context.SaveChangesAsync();

        // Debug: Verify shares were actually saved
        var savedShares = await _context.CooperativeShares.ToListAsync();
        Assert.Equal(3, savedShares.Count); // Should have 3 shares

        // Act
        var result = await _shareService.GenerateNextCertificateNumberAsync();

        // Assert
        Assert.Equal("CERT006", result); // Should be one more than the highest number (005)
    }

    [Fact]
    public async Task GenerateNextCertificateNumberAsync_WithMixedFormats_ShouldHandleCorrectly()
    {
        // Arrange - First create a member to reference
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "Test",
            LastName = "Member",
            Email = "test@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-1),
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };
        
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        
        var existingShares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                Id = 1,
                MemberId = 1,
                CertificateNumber = "CERT001",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new CooperativeShare
            {
                Id = 2,
                MemberId = 1,
                CertificateNumber = "CERT100",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new CooperativeShare
            {
                Id = 3,
                MemberId = 1,
                CertificateNumber = "INVALID",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-25),
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                UpdatedAt = DateTime.UtcNow.AddDays(-25)
            }
        };

        _context.CooperativeShares.AddRange(existingShares);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareService.GenerateNextCertificateNumberAsync();

        // Assert
        Assert.Equal("CERT101", result); // Should be one more than the highest valid number (100)
    }

    [Fact]
    public async Task GenerateNextCertificateNumberAsync_MultipleSequentialCalls_ShouldGenerateUniqueNumbers()
    {
        // Arrange - First create a member to reference
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "Test",
            LastName = "Member",
            Email = "test@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-1),
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };
        
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        
        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "CERT001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active,
            IssueDate = DateTime.UtcNow.AddDays(-30),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        };

        _context.CooperativeShares.Add(initialShare);
        await _context.SaveChangesAsync();

        // Act - Make sequential calls to test uniqueness
        var results = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var result = await _shareService.GenerateNextCertificateNumberAsync();
            results.Add(result);
            
            // Add the generated certificate to the context to simulate real usage
            _context.CooperativeShares.Add(new CooperativeShare
            {
                Id = i + 2,
                MemberId = 1,
                CertificateNumber = result,
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal(5, results.Distinct().Count()); // All should be unique
        
        // Verify all results are in the expected format and incrementing
        Assert.Equal("CERT002", results[0]);
        Assert.Equal("CERT003", results[1]);
        Assert.Equal("CERT004", results[2]);
        Assert.Equal("CERT005", results[3]);
        Assert.Equal("CERT006", results[4]);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}