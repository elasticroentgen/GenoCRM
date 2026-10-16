using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using GenoCRM.Services.Business.Messaging;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class MessagingServiceQueryTests : IDisposable
{
    private readonly GenoDbContext _context;
    private readonly MessagingService _service;

    public MessagingServiceQueryTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        _service = new MessagingService(_context, new Mock<IEmailProvider>().Object, new Mock<IWhatsAppProvider>().Object,
            new Mock<ISmsProvider>().Object, new Mock<ILogger<MessagingService>>().Object);
    }

    private async Task<Member> AddMemberWithShareAsync(string number, decimal paid)
    {
        var member = new Member { MemberNumber = number, FirstName = "A", LastName = number, Email = $"{number}@x.de", Status = MemberStatus.Active, JoinDate = DateTime.UtcNow };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();
        var share = new CooperativeShare { MemberId = member.Id, CertificateNumber = $"CERT{number}", Quantity = 2, NominalValue = 250m, Value = 250m, IssueDate = DateTime.UtcNow, Status = ShareStatus.Active };
        _context.CooperativeShares.Add(share);
        await _context.SaveChangesAsync();
        if (paid > 0)
        {
            _context.Payments.Add(new Payment { MemberId = member.Id, ShareId = share.Id, PaymentNumber = $"PAY{number}", Amount = paid, PaymentDate = DateTime.UtcNow, Status = PaymentStatus.Completed });
            await _context.SaveChangesAsync();
        }
        return member;
    }

    [Fact]
    public async Task GetMembersForPaymentReminders_ReturnsMembersWithUnpaidShares()
    {
        var unpaid = await AddMemberWithShareAsync("M001", 0m);
        var partlyPaid = await AddMemberWithShareAsync("M002", 250m);
        var fullyPaid = await AddMemberWithShareAsync("M003", 500m);

        var result = (await _service.GetMembersForPaymentRemindersAsync()).Select(m => m.Id).ToList();

        Assert.Contains(unpaid.Id, result);
        Assert.Contains(partlyPaid.Id, result);
        Assert.DoesNotContain(fullyPaid.Id, result);
    }

    public void Dispose() => _context.Dispose();
}
