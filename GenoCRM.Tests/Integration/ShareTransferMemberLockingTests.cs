using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Integration;

public class ShareTransferMemberLockingTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<ShareTransferService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IShareService> _mockShareService;
    private readonly ShareTransferService _shareTransferService;

    public ShareTransferMemberLockingTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<ShareTransferService>>();
        
        // Create a real configuration with in-memory values
        var inMemorySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:MaxSharesPerMember", "100"},
            {"CooperativeSettings:ShareDenomination", "250.00"}
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
            
        _mockShareService = new Mock<IShareService>();
        
        // Setup mock to return a certificate number
        _mockShareService.Setup(s => s.GenerateNextCertificateNumberAsync())
            .ReturnsAsync("CERT001");

        _shareTransferService = new ShareTransferService(_context, _mockLogger.Object, _mockShareService.Object, _configuration);
    }

    [Fact]
    public async Task FullWorkflow_MemberTransfersAllShares_ShouldBeLocked()
    {
        // Arrange - Create a complete scenario
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-12),
            CreatedAt = DateTime.UtcNow.AddMonths(-12),
            UpdatedAt = DateTime.UtcNow.AddMonths(-12)
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-6),
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            UpdatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        // Member has multiple shares, but will transfer them all
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                Id = 1,
                MemberId = 1,
                CertificateNumber = "CERT001",
                Quantity = 5,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddMonths(-12)
            },
            new CooperativeShare
            {
                Id = 2,
                MemberId = 1,
                CertificateNumber = "CERT002",
                Quantity = 3,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active,
                IssueDate = DateTime.UtcNow.AddMonths(-6)
            }
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.AddRange(shares);
        await _context.SaveChangesAsync();

        // Act - Create and complete transfers for all shares
        
        // Transfer 1: Transfer all 5 shares from first certificate
        var transfer1 = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 5,
            TotalValue = 1250.00m,
            Status = ShareTransferStatus.Approved,
            RequestDate = DateTime.UtcNow.AddHours(-2),
            ApprovalDate = DateTime.UtcNow.AddHours(-1),
            ApprovedBy = "Board Member"
        };

        _context.ShareTransfers.Add(transfer1);
        await _context.SaveChangesAsync();

        // Complete the first transfer
        var result1 = await _shareTransferService.CompleteShareTransferAsync(1);
        Assert.True(result1);

        // At this point, member should still be Active because they have 3 shares remaining
        var memberAfterFirstTransfer = await _context.Members.FindAsync(1);
        Assert.NotNull(memberAfterFirstTransfer);
        Assert.Equal(MemberStatus.Active, memberAfterFirstTransfer.Status);

        // Transfer 2: Transfer all 3 shares from second certificate
        var transfer2 = new ShareTransfer
        {
            Id = 2,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 2,
            Quantity = 3,
            TotalValue = 750.00m,
            Status = ShareTransferStatus.Approved,
            RequestDate = DateTime.UtcNow.AddMinutes(-30),
            ApprovalDate = DateTime.UtcNow.AddMinutes(-15),
            ApprovedBy = "Board Member"
        };

        _context.ShareTransfers.Add(transfer2);
        await _context.SaveChangesAsync();

        // Complete the second transfer - this should lock the member
        var result2 = await _shareTransferService.CompleteShareTransferAsync(2);
        Assert.True(result2);

        // Assert - Verify the member is now locked
        var finalMember = await _context.Members
            .Include(m => m.Shares)
            .FirstOrDefaultAsync(m => m.Id == 1);
        
        Assert.NotNull(finalMember);
        Assert.Equal(MemberStatus.Locked, finalMember.Status);
        
        // Verify the member has no active shares
        var activeShares = finalMember.Shares.Where(s => s.Status == ShareStatus.Active).Sum(s => s.Quantity);
        Assert.Equal(0, activeShares);
        
        // Verify both original shares are marked as transferred
        var originalShares = await _context.CooperativeShares
            .Where(s => s.MemberId == 1)
            .ToListAsync();
        
        Assert.Equal(2, originalShares.Count);
        Assert.All(originalShares, s => Assert.Equal(ShareStatus.Transferred, s.Status));
        
        // Verify the recipient received new shares
        var recipientShares = await _context.CooperativeShares
            .Where(s => s.MemberId == 2 && s.Status == ShareStatus.Active)
            .ToListAsync();
        
        Assert.Equal(2, recipientShares.Count);
        Assert.Equal(8, recipientShares.Sum(s => s.Quantity)); // 5 + 3 = 8 shares total
    }

    [Fact]
    public async Task MemberWith_InactiveStatus_ShouldNotBeLocked()
    {
        // Arrange - Create a member that is already inactive
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Status = MemberStatus.Inactive, // Already inactive
            JoinDate = DateTime.UtcNow.AddMonths(-12),
            CreatedAt = DateTime.UtcNow.AddMonths(-12),
            UpdatedAt = DateTime.UtcNow.AddMonths(-12)
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddMonths(-6),
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            UpdatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "CERT001",
            Quantity = 2,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active,
            IssueDate = DateTime.UtcNow.AddMonths(-12)
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 2, // Transfer all shares
            TotalValue = 500.00m,
            Status = ShareTransferStatus.Approved,
            RequestDate = DateTime.UtcNow.AddHours(-2),
            ApprovalDate = DateTime.UtcNow.AddHours(-1),
            ApprovedBy = "Board Member"
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CompleteShareTransferAsync(1);

        // Assert
        Assert.True(result);
        
        // Member should remain Inactive (not changed to Locked)
        var updatedMember = await _context.Members.FindAsync(1);
        Assert.NotNull(updatedMember);
        Assert.Equal(MemberStatus.Inactive, updatedMember.Status);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}