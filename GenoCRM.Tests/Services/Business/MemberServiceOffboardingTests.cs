using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using GenoCRM.Services.Integration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class MemberServiceOffboardingTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<IFiscalYearService> _fiscalYear = new();
    private readonly MemberService _memberService;

    public MemberServiceOffboardingTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);

        _fiscalYear.Setup(x => x.GetFiscalYearEnd(It.IsAny<int>())).Returns((int y) => new DateTime(y, 12, 31));
        _fiscalYear.Setup(x => x.GetFiscalYearForDate(It.IsAny<DateTime>())).Returns((DateTime d) => d.Year);
        // The fiscal year of a membership end has passed when that date lies in an earlier calendar year
        _fiscalYear.Setup(x => x.CanProcessOffboarding(It.IsAny<DateTime>())).Returns((DateTime d) => d.Year < DateTime.Today.Year);

        _memberService = new MemberService(_context, new Mock<ILogger<MemberService>>().Object, new Mock<IConfiguration>().Object,
            new Mock<IShareService>().Object, _fiscalYear.Object, new Mock<IAuditService>().Object,
            new Mock<IHttpContextAccessor>().Object, new Mock<IListmonkService>().Object);
    }

    private async Task<Member> AddOffboardingMemberAsync(DateTime? leaveDate, DividendStatus? dividendStatus = null)
    {
        var member = new Member
        {
            MemberNumber = Guid.NewGuid().ToString("N")[..6], FirstName = "A", LastName = "B", Email = $"{Guid.NewGuid():N}@x.de",
            Status = MemberStatus.Offboarding, JoinDate = DateTime.UtcNow.AddYears(-5),
            TerminationNoticeDate = DateTime.UtcNow.AddYears(-3), LeaveDate = leaveDate
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        if (dividendStatus.HasValue)
        {
            _context.Dividends.Add(new Dividend { MemberId = member.Id, FiscalYear = 2020, Amount = 1m, Rate = 0.01m, BaseAmount = 100m, Status = dividendStatus.Value, DeclarationDate = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }
        return member;
    }

    [Fact]
    public async Task OffboardMemberAsync_RecordsTheLegalMembershipEndAsLeaveDate()
    {
        var member = new Member
        {
            MemberNumber = "M001", FirstName = "A", LastName = "B", Email = "a@x.de", Status = MemberStatus.Active,
            JoinDate = DateTime.UtcNow.AddYears(-5), TerminationNoticeDate = new DateTime(DateTime.Today.Year - 3, 3, 15)
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        Assert.True(await _memberService.OffboardMemberAsync(member.Id));

        var expectedEnd = await _memberService.GetEarliestTerminationDateAsync(member.Id);
        Assert.Equal(MemberStatus.Offboarding, member.Status);
        Assert.Equal(expectedEnd, member.LeaveDate);
    }

    [Fact]
    public async Task GetMembersReadyForDeletion_ReturnsOnlyMembersWhoseMembershipEndYearHasPassed()
    {
        var ready = await AddOffboardingMemberAsync(new DateTime(DateTime.Today.Year - 1, 12, 31));
        var notYet = await AddOffboardingMemberAsync(new DateTime(DateTime.Today.Year, 12, 31));

        var result = (await _memberService.GetMembersReadyForDeletionAsync()).Select(m => m.Id).ToList();

        Assert.Contains(ready.Id, result);
        Assert.DoesNotContain(notYet.Id, result);
    }

    [Fact]
    public async Task FinallyDeleteMemberAsync_IgnoresCancelledDividends()
    {
        var member = await AddOffboardingMemberAsync(new DateTime(DateTime.Today.Year - 1, 12, 31), DividendStatus.Cancelled);

        Assert.True(await _memberService.FinallyDeleteMemberAsync(member.Id));
        Assert.Equal(MemberStatus.Terminated, member.Status);
    }

    [Fact]
    public async Task FinallyDeleteMemberAsync_StillBlocksOnUnpaidDividends()
    {
        var member = await AddOffboardingMemberAsync(new DateTime(DateTime.Today.Year - 1, 12, 31), DividendStatus.Approved);

        Assert.False(await _memberService.FinallyDeleteMemberAsync(member.Id));
    }

    [Fact]
    public async Task FinallyDeleteMemberAsync_RefusesWhileMembershipEndYearIsRunning()
    {
        var member = await AddOffboardingMemberAsync(new DateTime(DateTime.Today.Year, 12, 31));

        Assert.False(await _memberService.FinallyDeleteMemberAsync(member.Id));
    }

    public void Dispose() => _context.Dispose();
}
