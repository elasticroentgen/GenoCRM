using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareApprovalServiceTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<ILogger<ShareApprovalService>> _mockLogger;
    private readonly Mock<IShareService> _mockShareService;
    private readonly ShareApprovalService _shareApprovalService;

    public ShareApprovalServiceTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GenoDbContext(options);
        _mockLogger = new Mock<ILogger<ShareApprovalService>>();
        _mockShareService = new Mock<IShareService>();
        
        // Setup mock to return a certificate number
        _mockShareService.Setup(s => s.GenerateNextCertificateNumberAsync())
            .ReturnsAsync("CERT001");

        _shareApprovalService = new ShareApprovalService(_context, _mockLogger.Object, _mockShareService.Object);
    }

    [Fact]
    public async Task CreateShareApprovalRequestAsync_WithValidParameters_ShouldCreateApproval()
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

        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var payment = new Payment
        {
            Id = 1,
            MemberId = 1,
            ShareId = 1,
            Amount = 250.00m,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(initialShare);
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.CreateShareApprovalRequestAsync(1, 3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.MemberId);
        Assert.Equal(3, result.RequestedQuantity);
        Assert.Equal(750.00m, result.TotalValue);
        Assert.Equal(ShareApprovalStatus.Pending, result.Status);
        Assert.True(result.RequestDate <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateShareApprovalRequestAsync_WithInactiveMember_ShouldThrowException()
    {
        // Arrange
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M001",
            FirstName = "John",
            LastName = "Doe",
            Status = MemberStatus.Inactive
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shareApprovalService.CreateShareApprovalRequestAsync(1, 3));
    }

    [Fact]
    public async Task CreateShareApprovalRequestAsync_WithoutInitialShare_ShouldThrowException()
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _shareApprovalService.CreateShareApprovalRequestAsync(1, 3));
    }

    [Fact]
    public async Task CanRequestAdditionalSharesAsync_WithValidMember_ShouldReturnTrue()
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

        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var payment = new Payment
        {
            Id = 1,
            MemberId = 1,
            ShareId = 1,
            Amount = 250.00m,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(initialShare);
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.CanRequestAdditionalSharesAsync(1, 2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanRequestAdditionalSharesAsync_WithPendingRequest_ShouldReturnFalse()
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

        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var payment = new Payment
        {
            Id = 1,
            MemberId = 1,
            ShareId = 1,
            Amount = 250.00m,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow
        };

        var pendingApproval = new ShareApproval
        {
            Id = 1,
            MemberId = 1,
            RequestedQuantity = 2,
            TotalValue = 500.00m,
            Status = ShareApprovalStatus.Pending,
            RequestDate = DateTime.UtcNow
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(initialShare);
        _context.Payments.Add(payment);
        _context.ShareApprovals.Add(pendingApproval);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.CanRequestAdditionalSharesAsync(1, 2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ApproveShareRequestAsync_WithValidApproval_ShouldUpdateStatus()
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

        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var payment = new Payment
        {
            Id = 1,
            MemberId = 1,
            ShareId = 1,
            Amount = 250.00m,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow
        };

        var approval = new ShareApproval
        {
            Id = 1,
            MemberId = 1,
            RequestedQuantity = 2,
            TotalValue = 500.00m,
            Status = ShareApprovalStatus.Pending,
            RequestDate = DateTime.UtcNow
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(initialShare);
        _context.Payments.Add(payment);
        _context.ShareApprovals.Add(approval);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.ApproveShareRequestAsync(1, "admin");

        // Assert
        Assert.True(result);
        
        var updatedApproval = await _context.ShareApprovals.FindAsync(1);
        Assert.NotNull(updatedApproval);
        Assert.Equal(ShareApprovalStatus.Approved, updatedApproval.Status);
        Assert.Equal("admin", updatedApproval.ApprovedBy);
        Assert.NotNull(updatedApproval.ApprovalDate);
    }

    [Fact]
    public async Task RejectShareRequestAsync_WithValidApproval_ShouldUpdateStatus()
    {
        // Arrange
        var approval = new ShareApproval
        {
            Id = 1,
            MemberId = 1,
            RequestedQuantity = 2,
            TotalValue = 500.00m,
            Status = ShareApprovalStatus.Pending
        };

        _context.ShareApprovals.Add(approval);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.RejectShareRequestAsync(1, "admin", "Insufficient funds");

        // Assert
        Assert.True(result);
        
        var updatedApproval = await _context.ShareApprovals.FindAsync(1);
        Assert.NotNull(updatedApproval);
        Assert.Equal(ShareApprovalStatus.Rejected, updatedApproval.Status);
        Assert.Equal("admin", updatedApproval.ApprovedBy);
        Assert.Equal("Insufficient funds", updatedApproval.RejectionReason);
        Assert.NotNull(updatedApproval.ApprovalDate);
    }

    [Fact]
    public async Task CompleteShareApprovalAsync_WithApprovedRequest_ShouldCreateNewShare()
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

        var approval = new ShareApproval
        {
            Id = 1,
            MemberId = 1,
            RequestedQuantity = 2,
            TotalValue = 500.00m,
            Status = ShareApprovalStatus.Approved
        };

        _context.Members.Add(member);
        _context.ShareApprovals.Add(approval);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.CompleteShareApprovalAsync(1);

        // Assert
        Assert.True(result);
        
        var updatedApproval = await _context.ShareApprovals.FindAsync(1);
        Assert.NotNull(updatedApproval);
        Assert.Equal(ShareApprovalStatus.Completed, updatedApproval.Status);

        var newShares = await _context.CooperativeShares.Where(s => s.MemberId == 1).ToListAsync();
        Assert.Single(newShares);
        Assert.Equal(2, newShares[0].Quantity);
        Assert.Equal(250.00m, newShares[0].NominalValue);
        Assert.Equal(ShareStatus.Active, newShares[0].Status);
        Assert.Equal("CERT001", newShares[0].CertificateNumber); // Should use standard certificate format
    }

    [Fact]
    public async Task HasMemberCompletedInitialShareAsync_WithPaidShare_ShouldReturnTrue()
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

        var initialShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        var payment = new Payment
        {
            Id = 1,
            MemberId = 1,
            ShareId = 1,
            Amount = 250.00m,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(initialShare);
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.HasMemberCompletedInitialShareAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasMemberCompletedInitialShareAsync_WithoutPaidShare_ShouldReturnFalse()
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

        var unpaidShare = new CooperativeShare
        {
            Id = 1,
            MemberId = 1,
            CertificateNumber = "C001",
            Quantity = 1,
            NominalValue = 250.00m,
            Value = 250.00m,
            Status = ShareStatus.Active
        };

        _context.Members.Add(member);
        _context.CooperativeShares.Add(unpaidShare);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.HasMemberCompletedInitialShareAsync(1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPendingShareApprovalsAsync_ShouldReturnOnlyPendingApprovals()
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

        var approvals = new List<ShareApproval>
        {
            new ShareApproval
            {
                MemberId = 1,
                RequestedQuantity = 2,
                TotalValue = 500.00m,
                Status = ShareApprovalStatus.Pending,
                RequestDate = DateTime.UtcNow
            },
            new ShareApproval
            {
                MemberId = 1,
                RequestedQuantity = 1,
                TotalValue = 250.00m,
                Status = ShareApprovalStatus.Approved,
                RequestDate = DateTime.UtcNow
            },
            new ShareApproval
            {
                MemberId = 2,
                RequestedQuantity = 3,
                TotalValue = 750.00m,
                Status = ShareApprovalStatus.Pending,
                RequestDate = DateTime.UtcNow
            }
        };

        _context.Members.AddRange(members);
        _context.ShareApprovals.AddRange(approvals);
        await _context.SaveChangesAsync();

        // Act
        var result = await _shareApprovalService.GetPendingShareApprovalsAsync();

        // Assert
        var pendingApprovals = result.ToList();
        Assert.Equal(2, pendingApprovals.Count);
        Assert.All(pendingApprovals, a => Assert.Equal(ShareApprovalStatus.Pending, a.Status));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CanRequestAdditionalSharesAsync_WithInvalidQuantity_ShouldReturnFalse(int quantity)
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
        var result = await _shareApprovalService.CanRequestAdditionalSharesAsync(1, quantity);

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}