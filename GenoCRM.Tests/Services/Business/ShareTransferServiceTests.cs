using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareTransferServiceTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<ShareTransferService>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<IShareService> _mockShareService;
    private readonly ShareTransferService _shareTransferService;

    public ShareTransferServiceTests()
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
    public async Task CreateShareTransferRequestAsync_WithValidParameters_ShouldCreateTransfer()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CreateShareTransferRequestAsync(1, 2, 1, 3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FromMemberId);
        Assert.Equal(2, result.ToMemberId);
        Assert.Equal(1, result.ShareId);
        Assert.Equal(3, result.Quantity);
        Assert.Equal(750.00m, result.TotalValue);
        Assert.Equal(ShareTransferStatus.Pending, result.Status);
    }

    [Fact]
    public async Task CreateShareTransferRequestAsync_WithInvalidQuantity_ShouldThrowException()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 2,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _shareTransferService.CreateShareTransferRequestAsync(1, 2, 1, 5));
    }

    [Fact]
    public async Task ValidateShareTransferAsync_WithSameMember_ShouldReturnFalse()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.ValidateShareTransferAsync(1, 1, 1, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateShareTransferAsync_WithInactiveFromMember_ShouldReturnFalse()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Inactive
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        _context.Members.AddRange(fromMember, toMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.ValidateShareTransferAsync(1, 2, 1, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateShareTransferAsync_WithInactiveToMember_ShouldReturnFalse()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Terminated
        };

        _context.Members.AddRange(fromMember, toMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.ValidateShareTransferAsync(1, 2, 1, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateShareTransferAsync_WithValidParameters_ShouldReturnTrue()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.ValidateShareTransferAsync(1, 2, 1, 3);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ApproveShareTransferAsync_WithValidTransfer_ShouldUpdateStatus()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 3,
            TotalValue = 750.00m,
            Status = ShareTransferStatus.Pending
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.ApproveShareTransferAsync(1, "admin");

        // Assert
        Assert.True(result);
        
        var updatedTransfer = await _context.ShareTransfers.FindAsync(1);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(ShareTransferStatus.Approved, updatedTransfer.Status);
        Assert.Equal("admin", updatedTransfer.ApprovedBy);
        Assert.NotNull(updatedTransfer.ApprovalDate);
    }

    [Fact]
    public async Task CompleteShareTransferAsync_WithApprovedTransfer_ShouldCreateNewShareAndUpdateOriginal()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 3,
            TotalValue = 750.00m,
            Status = ShareTransferStatus.Approved
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CompleteShareTransferAsync(1);

        // Assert
        Assert.True(result);
        
        var updatedTransfer = await _context.ShareTransfers.FindAsync(1);
        Assert.NotNull(updatedTransfer);
        Assert.Equal(ShareTransferStatus.Completed, updatedTransfer.Status);
        Assert.NotNull(updatedTransfer.CompletionDate);

        var originalShare = await _context.CooperativeShares.FindAsync(1);
        Assert.NotNull(originalShare);
        Assert.Equal(2, originalShare.Quantity); // Reduced from 5 to 2

        var newShares = await _context.CooperativeShares.Where(s => s.MemberId == 2).ToListAsync();
        Assert.Single(newShares);
        Assert.Equal(3, newShares[0].Quantity);
        Assert.Equal(250.00m, newShares[0].NominalValue);
        Assert.Equal(ShareStatus.Active, newShares[0].Status);
        Assert.Equal("CERT001", newShares[0].CertificateNumber); // Should use standard certificate format
    }

    [Fact]
    public async Task CompleteShareTransferAsync_WithEntireShareTransfer_ShouldMarkOriginalAsTransferred()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 3,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 3, // Entire share
            TotalValue = 750.00m,
            Status = ShareTransferStatus.Approved
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CompleteShareTransferAsync(1);

        // Assert
        Assert.True(result);
        
        var originalShare = await _context.CooperativeShares.FindAsync(1);
        Assert.NotNull(originalShare);
        Assert.Equal(ShareStatus.Transferred, originalShare.Status);
        Assert.Equal(3, originalShare.Quantity); // Quantity unchanged
    }

    [Fact]
    public async Task GetPendingShareTransfersAsync_ShouldReturnOnlyPendingTransfers()
    {
        // Arrange
        var members = new List<Member>
        {
            new Member
            {
                Id = 1,
                MemberNumber = "M001",
                FirstName = "John",
                LastName = "Doe",
                Status = MemberStatus.Active
            },
            new Member
            {
                Id = 2,
                MemberNumber = "M002",
                FirstName = "Jane",
                LastName = "Smith",
                Status = MemberStatus.Active
            }
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfers = new List<ShareTransfer>
        {
            new ShareTransfer
            {
                Id = 1,
                FromMemberId = 1,
                ToMemberId = 2,
                ShareId = 1,
                Quantity = 1,
                TotalValue = 250.00m,
                Status = ShareTransferStatus.Pending,
                RequestDate = DateTime.UtcNow
            },
            new ShareTransfer
            {
                Id = 2,
                FromMemberId = 1,
                ToMemberId = 2,
                ShareId = 1,
                Quantity = 1,
                TotalValue = 250.00m,
                Status = ShareTransferStatus.Approved,
                RequestDate = DateTime.UtcNow
            },
            new ShareTransfer
            {
                Id = 3,
                FromMemberId = 1,
                ToMemberId = 2,
                ShareId = 1,
                Quantity = 1,
                TotalValue = 250.00m,
                Status = ShareTransferStatus.Pending,
                RequestDate = DateTime.UtcNow
            }
        };

        _context.Members.AddRange(members);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.AddRange(transfers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.GetPendingShareTransfersAsync();

        // Assert
        var pendingTransfers = result.ToList();
        Assert.Equal(2, pendingTransfers.Count);
        Assert.All(pendingTransfers, t => Assert.Equal(ShareTransferStatus.Pending, t.Status));
    }

    [Fact]
    public async Task CompleteShareTransferAsync_WhenMemberHasZeroSharesAfterTransfer_ShouldLockMember()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 3, // Member has only 3 shares
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 3, // Transferring all 3 shares
            TotalValue = 750.00m,
            Status = ShareTransferStatus.Approved
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CompleteShareTransferAsync(1);

        // Assert
        Assert.True(result);
        
        // Verify the member has been locked
        var updatedFromMember = await _context.Members.FindAsync(1);
        Assert.NotNull(updatedFromMember);
        Assert.Equal(MemberStatus.Locked, updatedFromMember.Status);
        
        // Verify the original share was marked as transferred
        var originalShare = await _context.CooperativeShares.FindAsync(1);
        Assert.NotNull(originalShare);
        Assert.Equal(ShareStatus.Transferred, originalShare.Status);
        
        // Verify a new share was created for the recipient
        var newShares = await _context.CooperativeShares.Where(s => s.MemberId == 2).ToListAsync();
        Assert.Single(newShares);
        Assert.Equal(3, newShares[0].Quantity);
        Assert.Equal(ShareStatus.Active, newShares[0].Status);
    }

    [Fact]
    public async Task CompleteShareTransferAsync_WhenMemberHasRemainingSharesAfterTransfer_ShouldNotLockMember()
    {
        // Arrange
        var fromMember = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Active
        };

        var toMember = new Member
        {
            Id = 2,
            MemberNumber = "M002",
            FirstName = "Jane",
            LastName = "Smith",
            Status = MemberStatus.Active
        };

        var share = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 5, // Member has 5 shares
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var transfer = new ShareTransfer
        {
            Id = 1,
            FromMemberId = 1,
            ToMemberId = 2,
            ShareId = 1,
            Quantity = 2, // Transferring only 2 shares, 3 remaining
            TotalValue = 500.00m,
            Status = ShareTransferStatus.Approved
        };

        _context.Members.AddRange(fromMember, toMember);
        _context.CooperativeShares.Add(share);
        _context.ShareTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareTransferService.CompleteShareTransferAsync(1);

        // Assert
        Assert.True(result);
        
        // Verify the member remains active (not locked)
        var updatedFromMember = await _context.Members.FindAsync(1);
        Assert.NotNull(updatedFromMember);
        Assert.Equal(MemberStatus.Active, updatedFromMember.Status);
        
        // Verify the original share quantity was reduced
        var originalShare = await _context.CooperativeShares.FindAsync(1);
        Assert.NotNull(originalShare);
        Assert.Equal(3, originalShare.Quantity); // 5 - 2 = 3
        Assert.Equal(ShareStatus.Active, originalShare.Status);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}