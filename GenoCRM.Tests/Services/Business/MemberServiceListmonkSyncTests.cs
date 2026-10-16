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

public class MemberServiceListmonkSyncTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly Mock<IListmonkService> _listmonk = new();
    private readonly MemberService _memberService;

    public MemberServiceListmonkSyncTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _memberService = new MemberService(_context, new Mock<ILogger<MemberService>>().Object, new Mock<IConfiguration>().Object,
            new Mock<IShareService>().Object, new Mock<IFiscalYearService>().Object, new Mock<IAuditService>().Object,
            new Mock<IHttpContextAccessor>().Object, _listmonk.Object);
    }

    private async Task<Member> AddMemberAsync()
    {
        var member = new Member { MemberNumber = "M001", FirstName = "A", LastName = "B", Email = "a@b.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task UpdateMemberAsync_SyncsToListmonkWhenStatusChanges_EvenOnTheTrackedInstance()
    {
        var member = await AddMemberAsync();

        // Blazor forms edit the tracked entity directly, so the "new" and "existing" objects are the same instance
        member.Status = MemberStatus.Suspended;
        await _memberService.UpdateMemberAsync(member);

        _listmonk.Verify(l => l.SyncMemberAsync(It.Is<Member>(m => m.Id == member.Id)), Times.Once);
    }

    [Fact]
    public async Task UpdateMemberAsync_DoesNotSyncWhenStatusIsUnchanged()
    {
        var member = await AddMemberAsync();

        member.Phone = "+49 171 1234567";
        await _memberService.UpdateMemberAsync(member);

        _listmonk.Verify(l => l.SyncMemberAsync(It.IsAny<Member>()), Times.Never);
    }

    public void Dispose() => _context.Dispose();
}
