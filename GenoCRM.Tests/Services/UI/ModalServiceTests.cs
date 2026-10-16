using Microsoft.Extensions.Localization;
using Moq;
using GenoCRM.Components.Shared;
using GenoCRM.Services.UI;
using Xunit;

namespace GenoCRM.Tests.Services.UI;

public class ModalServiceTests
{
    private static ModalService Create()
    {
        var localizer = new Mock<IStringLocalizer<GenoCRM.SharedResource>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string k) => new LocalizedString(k, k));
        return new ModalService(localizer.Object);
    }

    [Fact]
    public async Task Close_DoesNotWipeOutAModalOpenedByTheAwaitingContinuation()
    {
        var service = Create();
        var shown = new List<ModalReference>();
        var secondShown = new TaskCompletionSource();
        service.OnShow += r => { shown.Add(r); if (shown.Count == 2) secondShown.TrySetResult(); };

        // Caller awaits the first modal and immediately opens a second one in its continuation
        var chain = Task.Run(async () =>
        {
            await service.ShowConfirmationAsync("t", "m");
            await service.ShowAlertAsync("t2", "m2");
        });
        while (shown.Count == 0) await Task.Delay(10);

        service.Close(new ModalResult { Confirmed = true });
        await secondShown.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Closing again must complete the SECOND modal; with the old order the reference had been
        // nulled after the continuation ran, so this close was a no-op and the caller hung forever
        service.Close(new ModalResult { Confirmed = true });
        await chain.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
