using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class ShareServiceCreateShareTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly ShareService _shareService;

    public ShareServiceCreateShareTests()
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
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CreateShareAsync_RejectsNonPositiveQuantity(int quantity)
    {
        var member = new Member { MemberNumber = "M001", FirstName = "A", LastName = "B", Email = "a@b.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        var share = new CooperativeShare { MemberId = member.Id, Quantity = quantity, NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _shareService.CreateShareAsync(share));
        Assert.Equal(0, await _context.CooperativeShares.CountAsync());
    }

    public void Dispose() => _context.Dispose();
}
