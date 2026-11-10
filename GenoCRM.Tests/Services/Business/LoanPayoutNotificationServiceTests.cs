using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class LoanPayoutNotificationServiceTests
{
    private readonly Mock<IMessagingService> _messaging = new();
    private readonly LoanPayoutNotificationService _service;

    public LoanPayoutNotificationServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SepaDebtor:Name"] = "Bürgerenergie eG",
                ["Smtp:FromEmail"] = "darlehen@example.com",
                ["LoanPayoutNotification:PrivacyPolicyUrl"] = "https://example.com/datenschutz"
            })
            .Build();
        _service = new LoanPayoutNotificationService(_messaging.Object, config,
            new Mock<ILogger<LoanPayoutNotificationService>>().Object);
    }

    [Fact]
    public void BuildNotifications_GroupsPerMemberAndContainsOnlyMinimalData()
    {
        var anna = new Member { Id = 1, FirstName = "Anna", LastName = "Schmidt", Email = "anna@example.com", Street = "Geheimweg 1", BirthDate = new DateTime(1980, 5, 17) };
        var bob = new Member { Id = 2, FirstName = "Bob <b>", LastName = "Ziegler", Email = "" };
        var entries = new[]
        {
            Entry(anna, "LS0001", interest: 200m, principal: 0m, remaining: 5000m),
            Entry(anna, "LS0002", interest: 54.79m, principal: 5000m, remaining: 0m),
            Entry(bob, "LS0003", interest: 10m, principal: 0m, remaining: 1000m)
        };

        var result = _service.BuildNotifications(entries, _ => new DateTime(2026, 12, 31));

        Assert.Equal(2, result.Count);
        var annaMail = result[0];
        Assert.Same(anna, annaMail.Member);
        Assert.Equal(2, annaMail.Entries.Count);
        Assert.Equal("Anstehende Auszahlungen zu Ihren Darlehen", annaMail.Subject);
        Assert.Contains("Guten Tag Anna Schmidt,", annaMail.HtmlBody);
        Assert.Contains("Darlehen LS0001 – Stausee Oberwald", annaMail.HtmlBody);
        Assert.Contains("5.054,79 €", annaMail.HtmlBody);
        Assert.Contains("01.01.2026 – 31.12.2026 (365 Tage)", annaMail.HtmlBody);
        Assert.Contains("31.12.2026", annaMail.HtmlBody);
        Assert.Contains("4,00 % p. a.", annaMail.HtmlBody);
        Assert.Contains("DE** **** **** **** **30 00", annaMail.HtmlBody);
        Assert.Contains("an die Bürgerenergie eG", annaMail.HtmlBody);
        Assert.Contains("mailto:darlehen@example.com", annaMail.HtmlBody);
        Assert.Contains("https://example.com/datenschutz", annaMail.HtmlBody);
        Assert.Contains("ohne Steuerabzug", annaMail.HtmlBody);

        // Data minimisation: no full IBAN, address, birth date or gendered salutation
        Assert.DoesNotContain("DE89370400440532013000", annaMail.HtmlBody);
        Assert.DoesNotContain("Geheimweg", annaMail.HtmlBody);
        Assert.DoesNotContain("1980", annaMail.HtmlBody);
        Assert.DoesNotContain("Frau", annaMail.HtmlBody);
        Assert.DoesNotContain("Herr", annaMail.HtmlBody);

        var bobMail = result[1];
        Assert.False(bobMail.HasEmail);
        Assert.Equal("Anstehende Auszahlung zu Ihrem Darlehen LS0003", bobMail.Subject);
        Assert.Contains("Bob &lt;b&gt; Ziegler", bobMail.HtmlBody);
    }

    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 00", "DE** **** **** **** **30 00")]
    [InlineData("at611904300234573201", "AT** **** **** **** 3201")]
    [InlineData("DE12", "****")]
    public void MaskIban_KeepsCountryCodeAndLastFourDigits(string iban, string expected)
    {
        Assert.Equal(expected, LoanPayoutNotificationService.MaskIban(iban));
    }

    [Fact]
    public async Task SendAsync_SendsOneMessagePerMemberWithEmailAndReportsOutcome()
    {
        var ok = new Member { Id = 1, FirstName = "A", LastName = "Ok", Email = "a@example.com" };
        var optedOut = new Member { Id = 2, FirstName = "B", LastName = "Out", Email = "b@example.com" };
        var failing = new Member { Id = 3, FirstName = "C", LastName = "Fail", Email = "c@example.com" };
        var noMail = new Member { Id = 4, FirstName = "D", LastName = "NoMail", Email = " " };
        SetupSend(1, MessageStatus.Sent);
        SetupSend(2, MessageStatus.Cancelled);
        SetupSend(3, MessageStatus.Failed, "SMTP down");

        var notifications = _service.BuildNotifications(
            new[] { ok, optedOut, failing, noMail }.Select((m, i) => Entry(m, $"LS{i}", 1m, 0m, 0m)),
            _ => DateTime.Today);
        var result = await _service.SendAsync(notifications);

        Assert.Equal(1, result.Sent);
        Assert.Equal("B Out", Assert.Single(result.OptedOut));
        Assert.Equal("C Fail: SMTP down", Assert.Single(result.Failed));
        Assert.Equal("D NoMail", Assert.Single(result.WithoutEmail));
        _messaging.Verify(m => m.SendMessageAsync(4, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<MessageType>(), It.IsAny<MessageChannel>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        _messaging.Verify(m => m.SendMessageAsync(1, It.IsAny<string>(), It.IsAny<string>(),
            MessageType.LoanPayoutNotification, MessageChannel.Email, It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    private void SetupSend(int memberId, MessageStatus status, string? error = null) =>
        _messaging.Setup(m => m.SendMessageAsync(memberId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<MessageType>(), It.IsAny<MessageChannel>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new Message { MemberId = memberId, Status = status, ErrorMessage = error });

    private static LoanPaymentPlanEntry Entry(Member member, string number, decimal interest, decimal principal, decimal remaining) => new()
    {
        DueDate = new DateTime(2026, 12, 31),
        InterestAmount = interest,
        PrincipalAmount = principal,
        TotalAmount = interest + principal,
        RemainingBalance = remaining,
        InterestPeriodStart = new DateTime(2026, 1, 1),
        InterestPeriodEnd = new DateTime(2027, 1, 1),
        InterestDays = 365,
        LoanPaymentPlan = new LoanPaymentPlan
        {
            LoanSubscription = new LoanSubscription
            {
                SubscriptionNumber = number,
                Amount = 5000m,
                IBAN = "DE89370400440532013000",
                Member = member,
                LoanOffer = new LoanOffer { InterestRate = 0.04m, LoanProject = new LoanProject { Title = "Stausee Oberwald" } }
            }
        }
    };
}
