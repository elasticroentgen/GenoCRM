using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class DividendServiceTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly DividendService _service;
    private readonly int _closedYear = DateTime.UtcNow.Year - 1;

    public DividendServiceTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _service = new DividendService(_context, new Mock<ILogger<DividendService>>().Object);
    }

    private async Task<Member> AddActiveMemberWithShareAsync(string number)
    {
        var member = new Member
        {
            MemberNumber = number, FirstName = "A", LastName = number, Email = $"{number}@x.de",
            Status = MemberStatus.Active, JoinDate = DateTime.UtcNow.AddYears(-3)
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        _context.CooperativeShares.Add(new CooperativeShare
        {
            MemberId = member.Id, CertificateNumber = $"CERT{number}", Quantity = 4, NominalValue = 250m, Value = 250m,
            IssueDate = DateTime.UtcNow.AddYears(-3), Status = ShareStatus.Active
        });
        await _context.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task CalculateDividendsForAllMembers_CreatesOneDividendPerMember()
    {
        await AddActiveMemberWithShareAsync("M001");
        await AddActiveMemberWithShareAsync("M002");

        var result = (await _service.CalculateDividendsForAllMembersAsync(_closedYear, 0.03m)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(30m, d.Amount)); // 4 * 250 * 0.03
    }

    [Fact]
    public async Task CalculateDividendsForAllMembers_RefusesToRunTwiceForSameYear()
    {
        await AddActiveMemberWithShareAsync("M001");
        await _service.CalculateDividendsForAllMembersAsync(_closedYear, 0.03m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CalculateDividendsForAllMembersAsync(_closedYear, 0.03m));

        Assert.Equal(1, await _context.Dividends.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(3)]      // "0.03" parsed under de-DE
    [InlineData(0.25)]
    public async Task CalculateDividendsForAllMembers_RejectsImplausibleRates(double rate)
    {
        await AddActiveMemberWithShareAsync("M001");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CalculateDividendsForAllMembersAsync(_closedYear, (decimal)rate));
    }

    [Fact]
    public async Task CalculateDividendsForAllMembers_RejectsOpenFiscalYear()
    {
        await AddActiveMemberWithShareAsync("M001");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CalculateDividendsForAllMembersAsync(DateTime.UtcNow.Year, 0.03m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CalculateDividendsForAllMembersAsync(0, 0.03m));
    }

    [Fact]
    public async Task GetTotalDividendsByYear_ExcludesCancelledDividends()
    {
        var member = await AddActiveMemberWithShareAsync("M001");
        _context.Dividends.AddRange(
            new Dividend { MemberId = member.Id, FiscalYear = _closedYear, Amount = 30m, Rate = 0.03m, BaseAmount = 1000m, Status = DividendStatus.Approved, DeclarationDate = DateTime.UtcNow },
            new Dividend { MemberId = member.Id, FiscalYear = _closedYear, Amount = 99m, Rate = 0.03m, BaseAmount = 1000m, Status = DividendStatus.Cancelled, DeclarationDate = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        Assert.Equal(30m, await _service.GetTotalDividendsByYearAsync(_closedYear));
    }

    public void Dispose() => _context.Dispose();
}
