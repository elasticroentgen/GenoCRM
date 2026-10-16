using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using GenoCRM.Services.Business.Messaging;
using Xunit;

namespace GenoCRM.Tests.Services.Messaging;

public class PhoneValidationTests
{
    private static SmsProvider Sms() =>
        new(Options.Create(new SmsSettings()), new HttpClient(), new Mock<ILogger<SmsProvider>>().Object);

    private static WhatsAppProvider WhatsApp() =>
        new(Options.Create(new WhatsAppSettings()), new HttpClient(), new Mock<ILogger<WhatsAppProvider>>().Object);

    [Theory]
    [InlineData("0171 1234567")]
    [InlineData("(0171) 123-4567")]
    [InlineData("+49 171 1234567")]
    [InlineData("0049171123456")]
    public async Task NationalGermanNumbers_AreAccepted(string number)
    {
        Assert.True(await Sms().ValidatePhoneNumberAsync(number));
        Assert.True(await WhatsApp().ValidatePhoneNumberAsync(number));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("+")]
    public async Task Garbage_IsRejected(string number)
    {
        Assert.False(await Sms().ValidatePhoneNumberAsync(number));
        Assert.False(await WhatsApp().ValidatePhoneNumberAsync(number));
    }
}
