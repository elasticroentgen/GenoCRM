using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

/// <summary>Cancellation, transfer completion and consolidation must respect each other's state.</summary>
public class ShareStateMachineTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly ShareService _shareService;
    private readonly ShareTransferService _transferService;
    private readonly ShareConsolidationService _consolidationService;
    private readonly Mock<IAuditService> _audit = new();
    private int _certSeq = 100;

    public ShareStateMachineTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new GenoDbContext(options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"CooperativeSettings:ShareDenomination", "250.00"},
                {"CooperativeSettings:MaxSharesPerMember", "100"}
            })
            .Build();
        _shareService = new ShareService(_context, new Mock<ILogger<ShareService>>().Object, configuration);
        var certGen = new Mock<IShareService>();
        certGen.Setup(s => s.GenerateNextCertificateNumberAsync()).ReturnsAsync(() => $"CERT{++_certSeq}");
        _transferService = new ShareTransferService(_context, new Mock<ILogger<ShareTransferService>>().Object, certGen.Object, configuration);
        _consolidationService = new ShareConsolidationService(_context, new Mock<ILogger<ShareConsolidationService>>().Object,
            certGen.Object, _audit.Object, new Mock<IHttpContextAccessor>().Object);
    }

    private async Task<Member> AddMemberAsync(string number)
    {
        var m = new Member { MemberNumber = number, FirstName = "A", LastName = number, Email = $"{number}@x.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow.AddYears(-3) };
        _context.Members.Add(m);
        await _context.SaveChangesAsync();
        return m;
    }

    private async Task<CooperativeShare> AddShareAsync(Member member, int quantity, bool paid, ShareStatus status = ShareStatus.Active)
    {
        var share = new CooperativeShare
        {
            MemberId = member.Id, CertificateNumber = $"C{++_certSeq}", Quantity = quantity, NominalValue = 250m, Value = 250m,
            IssueDate = DateTime.UtcNow.AddYears(-2), Status = status
        };
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();
        if (paid)
        {
            _context.Payments.Add(new Payment { MemberId = member.Id, ShareId = share.Id, PaymentNumber = $"P{share.Id}", Amount = quantity * 250m, PaymentDate = DateTime.UtcNow, Status = PaymentStatus.Completed });
            await _context.SaveChangesAsync();
        }
        return share;
    }

    private async Task<ShareTransfer> AddTransferAsync(CooperativeShare share, Member to, int quantity, ShareTransferStatus status)
    {
        var t = new ShareTransfer { FromMemberId = share.MemberId, ToMemberId = to.Id, ShareId = share.Id, Quantity = quantity, TotalValue = quantity * 250m, Status = status, RequestDate = DateTime.UtcNow };
        _context.ShareTransfers.Add(t);
        await _context.SaveChangesAsync();
        return t;
    }

    // --- cancellation ---------------------------------------------------------

    [Fact]
    public async Task CancelShareAsync_RefusesUnpaidShare()
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, paid: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _shareService.CancelShareAsync(share.Id));
        Assert.Equal(ShareStatus.Active, share.Status);
    }

    [Theory]
    [InlineData(ShareStatus.Transferred)]
    [InlineData(ShareStatus.Cancelled)]
    public async Task CancelShareAsync_RefusesNonActiveShare(ShareStatus status)
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, paid: true, status: status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _shareService.CancelShareAsync(share.Id));
    }

    [Fact]
    public async Task CancelShareAsync_DefaultsCancellationDateToTwoYearNoticePeriod()
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, paid: true);

        Assert.True(await _shareService.CancelShareAsync(share.Id));

        Assert.Equal(ShareStatus.Cancelled, share.Status);
        Assert.Equal(DateTime.Today.AddYears(2), share.CancellationDate!.Value.Date);
    }

    [Fact]
    public async Task CancelShareAsync_RejectsCancellationDateInsideNoticePeriod()
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, paid: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _shareService.CancelShareAsync(share.Id, DateTime.Today.AddYears(1)));
        Assert.True(await _shareService.CancelShareAsync(share.Id, DateTime.Today.AddYears(2)));
    }

    // --- transfers ------------------------------------------------------------

    [Fact]
    public async Task ValidateShareTransferAsync_SubtractsQuantityReservedByOpenTransfers()
    {
        var from = await AddMemberAsync("M001");
        var to = await AddMemberAsync("M002");
        var share = await AddShareAsync(from, 5, paid: true);
        await AddTransferAsync(share, to, 3, ShareTransferStatus.Pending);

        Assert.False(await _transferService.ValidateShareTransferAsync(from.Id, to.Id, share.Id, 3));
        Assert.True(await _transferService.ValidateShareTransferAsync(from.Id, to.Id, share.Id, 2));
    }

    [Fact]
    public async Task CompleteShareTransferAsync_RefusesWhenTheShareNoLongerCoversTheQuantity()
    {
        var from = await AddMemberAsync("M001");
        var to = await AddMemberAsync("M002");
        var share = await AddShareAsync(from, 5, paid: true);
        var first = await AddTransferAsync(share, to, 3, ShareTransferStatus.Approved);
        var second = await AddTransferAsync(share, to, 3, ShareTransferStatus.Approved);

        Assert.True(await _transferService.CompleteShareTransferAsync(first.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _transferService.CompleteShareTransferAsync(second.Id));

        Assert.Equal(2, share.Quantity);
        Assert.Equal(ShareTransferStatus.Approved, second.Status);
    }

    [Fact]
    public async Task CompleteShareTransferAsync_RefusesWhenTheShareIsNoLongerActive()
    {
        var from = await AddMemberAsync("M001");
        var to = await AddMemberAsync("M002");
        var share = await AddShareAsync(from, 5, paid: true);
        var transfer = await AddTransferAsync(share, to, 5, ShareTransferStatus.Approved);
        share.Status = ShareStatus.Transferred;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _transferService.CompleteShareTransferAsync(transfer.Id));
        Assert.Equal(0, await _context.CooperativeShares.CountAsync(s => s.MemberId == to.Id));
    }

    [Fact]
    public async Task CreateShareTransferRequestAsync_StoresTheNotes()
    {
        var from = await AddMemberAsync("M001");
        var to = await AddMemberAsync("M002");
        var share = await AddShareAsync(from, 5, paid: true);

        var transfer = await _transferService.CreateShareTransferRequestAsync(from.Id, to.Id, share.Id, 2, "Gift to daughter");

        Assert.Equal("Gift to daughter", transfer.Notes);
    }

    // --- consolidation --------------------------------------------------------

    [Theory]
    [InlineData(ShareTransferStatus.Pending)]
    [InlineData(ShareTransferStatus.Approved)]
    public async Task ValidateConsolidationAsync_BlocksSharesWithOpenTransfers(ShareTransferStatus status)
    {
        var m = await AddMemberAsync("M001");
        var to = await AddMemberAsync("M002");
        var a = await AddShareAsync(m, 2, paid: true);
        var b = await AddShareAsync(m, 3, paid: true);
        await AddTransferAsync(a, to, 1, status);

        var result = await _consolidationService.ValidateConsolidationAsync(new[] { a.Id, b.Id });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ConsolidateSharesAsync_LogsAuditEntriesUnderTheConsolidatePermission()
    {
        var m = await AddMemberAsync("M001");
        var a = await AddShareAsync(m, 2, paid: true);
        var b = await AddShareAsync(m, 3, paid: true);

        await _consolidationService.ConsolidateSharesAsync(m.Id, new[] { a.Id, b.Id }, "test");

        _audit.Verify(x => x.LogActionAsync(It.IsAny<string>(), It.IsAny<AuditAction>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string>(), Permissions.TransferShares, It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        _audit.Verify(x => x.LogActionAsync(It.IsAny<string>(), It.IsAny<AuditAction>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string>(), Permissions.ConsolidateShares, It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.AtLeast(2));
    }

    public void Dispose() => _context.Dispose();
}
