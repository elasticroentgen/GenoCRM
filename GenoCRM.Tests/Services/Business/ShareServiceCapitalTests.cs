using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareServiceCapitalTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<ShareService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly ShareService _shareService;

    public ShareServiceCapitalTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<ShareService>>();
        
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
    public async Task GetActiveSharesAsync_ShouldReturnOnlyActiveShares()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        var shares = await CreateTestSharesAsync(member.Id);
        
        // Act
        var result = await _shareService.GetActiveSharesAsync();
        
        // Assert
        var activeShares = result.ToList();
        Assert.Single(activeShares); // Only one active share
        Assert.Equal(ShareStatus.Active, activeShares[0].Status);
        Assert.Equal("CERT001", activeShares[0].CertificateNumber);
    }

    [Fact]
    public async Task GetNonActiveSharesAsync_ShouldReturnNonActiveShares()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        var shares = await CreateTestSharesAsync(member.Id);
        
        // Act
        var result = await _shareService.GetNonActiveSharesAsync();
        
        // Assert
        var nonActiveShares = result.ToList();
        Assert.Equal(3, nonActiveShares.Count); // Cancelled, Transferred, Suspended
        Assert.Contains(nonActiveShares, s => s.Status == ShareStatus.Cancelled);
        Assert.Contains(nonActiveShares, s => s.Status == ShareStatus.Transferred);
        Assert.Contains(nonActiveShares, s => s.Status == ShareStatus.Suspended);
        Assert.DoesNotContain(nonActiveShares, s => s.Status == ShareStatus.Active);
    }

    [Fact]
    public async Task GetActiveShareCapitalAsync_ShouldReturnCorrectAmount()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        
        // Create mix of active fully paid and unpaid shares
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT001",
                Quantity = 2,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 500.00m, // Fully paid
                        PaymentDate = DateTime.UtcNow.AddDays(-25),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            },
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT002",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 100.00m, // Partially paid
                        PaymentDate = DateTime.UtcNow.AddDays(-15),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            },
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT003",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Cancelled, // Not active
                IssueDate = DateTime.UtcNow.AddDays(-10),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 250.00m, // Fully paid but cancelled
                        PaymentDate = DateTime.UtcNow.AddDays(-5),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            }
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();
        
        // Act
        var result = await _shareService.GetActiveShareCapitalAsync();
        
        // Assert
        // Only fully paid active shares: CERT001 (2 * 250) = 500
        Assert.Equal(500.00m, result);
    }

    [Fact]
    public async Task GetUnpaidShareCapitalAsync_ShouldReturnCorrectAmount()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT001",
                Quantity = 2,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 500.00m, // Fully paid
                        PaymentDate = DateTime.UtcNow.AddDays(-25),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            },
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT002",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 100.00m, // Partially paid
                        PaymentDate = DateTime.UtcNow.AddDays(-15),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            },
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT003",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-10),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
                // No payments - completely unpaid
            }
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();
        
        // Act
        var result = await _shareService.GetUnpaidShareCapitalAsync();
        
        // Assert
        // Unpaid active shares: CERT002 (1 * 250) + CERT003 (1 * 250) = 500
        Assert.Equal(500.00m, result);
    }

    [Fact]
    public async Task GetOffboardingSharesValueAsync_ShouldReturnCorrectAmount()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        var shares = await CreateTestSharesAsync(member.Id);
        
        // Act
        var result = await _shareService.GetOffboardingSharesValueAsync();
        
        // Assert
        // Cancelled (1 * 250) + Transferred (1 * 250) + Suspended (1 * 250) = 750
        Assert.Equal(750.00m, result);
    }

    [Fact]
    public async Task GetActiveSharesAsync_WithNoActiveShares_ShouldReturnEmpty()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT001",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Cancelled,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();
        
        // Act
        var result = await _shareService.GetActiveSharesAsync();
        
        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnpaidShareCapitalAsync_WithAllSharesPaid_ShouldReturnZero()
    {
        // Arrange
        var member = await CreateTestMemberAsync();
        
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = member.Id,
                CertificateNumber = "CERT001",
                Quantity = 2,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Payments = new List<Payment>
                {
                    new Payment
                    {
                        Amount = 500.00m, // Fully paid
                        PaymentDate = DateTime.UtcNow.AddDays(-25),
                        Method = PaymentMethod.BankTransfer,
                        Status = PaymentStatus.Completed,
                        Type = PaymentType.ShareCapital,
                        PaymentNumber = $"PAY{Guid.NewGuid().ToString()[..8]}",
                        MemberId = member.Id
                    }
                }
            }
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();
        
        // Act
        var result = await _shareService.GetUnpaidShareCapitalAsync();
        
        // Assert
        Assert.Equal(0.00m, result);
    }

    private async Task<Member> CreateTestMemberAsync()
    {
        var member = new Member
        {
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
        return member;
    }

    private async Task<List<CooperativeShare>> CreateTestSharesAsync(int memberId)
    {
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = memberId,
                CertificateNumber = "CERT001",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CooperativeShare
            {
                MemberId = memberId,
                CertificateNumber = "CERT002",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Cancelled,
                IssueDate = DateTime.UtcNow.AddDays(-20),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CooperativeShare
            {
                MemberId = memberId,
                CertificateNumber = "CERT003",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Transferred,
                IssueDate = DateTime.UtcNow.AddDays(-25),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CooperativeShare
            {
                MemberId = memberId,
                CertificateNumber = "CERT004",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Suspended,
                IssueDate = DateTime.UtcNow.AddDays(-15),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();
        return shares;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}