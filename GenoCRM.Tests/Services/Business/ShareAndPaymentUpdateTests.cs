using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using GenoCRM.Services.Integration;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareAndPaymentUpdateTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly ShareService _shareService;
    private readonly PaymentService _paymentService;
    private readonly MemberService _memberService;

    public ShareAndPaymentUpdateTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"},
            {"CooperativeSettings:MaxSharesPerMember", "5"}
        }).Build();
        _shareService = new ShareService(_context, new Mock<ILogger<ShareService>>().Object, configuration);
        _paymentService = new PaymentService(_context, new Mock<ILogger<PaymentService>>().Object);
        _memberService = new MemberService(_context, new Mock<ILogger<MemberService>>().Object, configuration,
            _shareService, new Mock<IFiscalYearService>().Object, new Mock<IAuditService>().Object,
            new Mock<IHttpContextAccessor>().Object, new Mock<IListmonkService>().Object);
    }

    private async Task<Member> AddMemberAsync(string number, MemberType type = MemberType.Individual, string? company = null, string? contact = null)
    {
        var m = new Member
        {
            MemberNumber = number, FirstName = type == MemberType.Individual ? "Anna" : "", LastName = type == MemberType.Individual ? "Schmidt" : "",
            CompanyName = company ?? string.Empty, ContactPerson = contact, MemberType = type,
            Email = $"{number}@x.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow
        };
        _context.Members.Add(m);
        await _context.SaveChangesAsync();
        return m;
    }

    private async Task<CooperativeShare> AddShareAsync(Member member, int quantity, string cert)
    {
        var s = new CooperativeShare { MemberId = member.Id, CertificateNumber = cert, Quantity = quantity, NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active };
        _context.CooperativeShares.Add(s);
        await _context.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task RecordSharePaymentAsync_CanRecordAPendingPaymentWithNotes()
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, "C1");

        var payment = await _paymentService.RecordSharePaymentAsync(share.Id, 100m, PaymentMethod.BankTransfer, "ref", DateTime.Today, notes: "awaiting bank", markAsCompleted: false);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.ProcessedDate);
        Assert.Equal("awaiting bank", payment.Notes);
    }

    [Fact]
    public async Task RecordSharePaymentAsync_DefaultsToCompleted()
    {
        var m = await AddMemberAsync("M001");
        var share = await AddShareAsync(m, 2, "C1");

        var payment = await _paymentService.RecordSharePaymentAsync(share.Id, 100m, PaymentMethod.Cash);

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.NotNull(payment.ProcessedDate);
    }

    [Fact]
    public async Task UpdateShareAsync_EnforcesMaxSharesAcrossTheMembersCertificates()
    {
        var m = await AddMemberAsync("M001");
        var other = await AddShareAsync(m, 3, "C1");
        var share = await AddShareAsync(m, 1, "C2");

        var tooMany = new CooperativeShare { Id = share.Id, CertificateNumber = "C2", Quantity = 3, NominalValue = 250m, Value = 250m, Status = ShareStatus.Active }; // 3 + 3 > 5
        await Assert.ThrowsAsync<InvalidOperationException>(() => _shareService.UpdateShareAsync(tooMany));

        var justRight = new CooperativeShare { Id = share.Id, CertificateNumber = "C2", Quantity = 2, NominalValue = 250m, Value = 250m, Status = ShareStatus.Active }; // 3 + 2 = 5
        await _shareService.UpdateShareAsync(justRight);
        Assert.Equal(2, share.Quantity);
    }

    [Fact]
    public async Task SearchMembersAsync_FindsCompaniesByNameAndContactPerson()
    {
        await AddMemberAsync("M001");
        var company = await AddMemberAsync("M002", MemberType.Company, company: "Sonnenkraft eG", contact: "Petra Müller");

        var byName = (await _memberService.SearchMembersAsync("sonnen")).Select(x => x.Id).ToList();
        var byContact = (await _memberService.SearchMembersAsync("müller")).Select(x => x.Id).ToList();

        Assert.Equal(new[] { company.Id }, byName);
        Assert.Equal(new[] { company.Id }, byContact);
    }

    public void Dispose() => _context.Dispose();
}
