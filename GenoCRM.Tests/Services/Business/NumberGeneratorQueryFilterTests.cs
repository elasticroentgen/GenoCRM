using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

/// <summary>
/// Certificate and payment numbers are unique across ALL members, including
/// terminated ones that the global query filters hide. The generators must
/// therefore look past the filters, otherwise they re-issue a number that
/// already exists and every later insert fails on the unique index.
/// </summary>
public class NumberGeneratorQueryFilterTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly ShareService _shareService;
    private readonly PaymentService _paymentService;

    public NumberGeneratorQueryFilterTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
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
        _paymentService = new PaymentService(_context, new Mock<ILogger<PaymentService>>().Object);
    }

    private async Task<Member> AddMemberAsync(string number, MemberStatus status)
    {
        var member = new Member
        {
            MemberNumber = number,
            FirstName = "Test",
            LastName = number,
            Email = $"{number}@example.com",
            Status = status,
            JoinDate = DateTime.UtcNow
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task GenerateNextCertificateNumberAsync_SkipsNumbersHeldByTerminatedMembers()
    {
        var terminated = await AddMemberAsync("M001", MemberStatus.Terminated);
        _context.CooperativeShares.Add(new CooperativeShare
        {
            MemberId = terminated.Id, CertificateNumber = "CERT005", Quantity = 1,
            NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active
        });
        await _context.SaveChangesAsync();

        var next = await _shareService.GenerateNextCertificateNumberAsync();

        Assert.Equal("CERT006", next);
    }

    [Fact]
    public async Task CertificateNumberExistsAsync_SeesTerminatedMembersCertificates()
    {
        var terminated = await AddMemberAsync("M001", MemberStatus.Terminated);
        _context.CooperativeShares.Add(new CooperativeShare
        {
            MemberId = terminated.Id, CertificateNumber = "CERT005", Quantity = 1,
            NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active
        });
        await _context.SaveChangesAsync();

        Assert.True(await _shareService.CertificateNumberExistsAsync("CERT005"));
    }

    [Fact]
    public async Task GenerateNextPaymentNumberAsync_SkipsNumbersHeldByTerminatedMembers()
    {
        var terminated = await AddMemberAsync("M001", MemberStatus.Terminated);
        var share = new CooperativeShare
        {
            MemberId = terminated.Id, CertificateNumber = "CERT001", Quantity = 1,
            NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active
        };
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();

        var year = DateTime.UtcNow.Year;
        _context.Payments.Add(new Payment
        {
            MemberId = terminated.Id, ShareId = share.Id, PaymentNumber = $"PAY{year}0007",
            Amount = 250m, PaymentDate = DateTime.UtcNow, Status = PaymentStatus.Completed
        });
        await _context.SaveChangesAsync();

        var next = await _paymentService.GenerateNextPaymentNumberAsync();

        Assert.Equal($"PAY{year}0008", next);
    }

    [Fact]
    public async Task PaymentNumberExistsAsync_SeesTerminatedMembersPayments()
    {
        var terminated = await AddMemberAsync("M001", MemberStatus.Terminated);
        var share = new CooperativeShare
        {
            MemberId = terminated.Id, CertificateNumber = "CERT001", Quantity = 1,
            NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active
        };
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();
        _context.Payments.Add(new Payment
        {
            MemberId = terminated.Id, ShareId = share.Id, PaymentNumber = "PAY20200001",
            Amount = 250m, PaymentDate = DateTime.UtcNow, Status = PaymentStatus.Completed
        });
        await _context.SaveChangesAsync();

        Assert.True(await _paymentService.PaymentNumberExistsAsync("PAY20200001"));
    }

    public void Dispose() => _context.Dispose();
}
