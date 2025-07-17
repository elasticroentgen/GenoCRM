using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using GenoCRM.Components.Shared;

namespace GenoCRM.Services.UI;

public class ModalService : IModalService
{
    private readonly IStringLocalizer<SharedResource> _localizer;
    private ModalReference? _currentModalReference;
    
    public ModalService(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }
    
    public event Action<ModalReference>? OnShow;
    public event Action? OnClose;

    public Task<ModalResult> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null) where T : ComponentBase
    {
        return ShowAsync(typeof(T), title, parameters);
    }

    public Task<ModalResult> ShowAsync(Type componentType, string title, Dictionary<string, object>? parameters = null)
    {
        var modalReference = new ModalReference
        {
            ComponentType = componentType,
            Title = title,
            Parameters = parameters ?? new Dictionary<string, object>()
        };

        _currentModalReference = modalReference;
        OnShow?.Invoke(modalReference);
        return modalReference.TaskCompletionSource.Task;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "", string cancelText = "")
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            { "ConfirmText", string.IsNullOrEmpty(confirmText) ? _localizer["Yes"] : confirmText },
            { "CancelText", string.IsNullOrEmpty(cancelText) ? _localizer["No"] : cancelText }
        };

        var result = await ShowAsync(typeof(ConfirmationModal), title, parameters);
        return result.Confirmed;
    }

    public async Task ShowAlertAsync(string title, string message, string closeText = "")
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            { "CloseText", string.IsNullOrEmpty(closeText) ? _localizer["OK"] : closeText }
        };

        await ShowAsync(typeof(AlertModal), title, parameters);
    }

    public async Task ShowErrorAsync(string title, string message, string closeText = "")
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            { "CloseText", string.IsNullOrEmpty(closeText) ? _localizer["OK"] : closeText },
            { "IsError", true }
        };

        await ShowAsync(typeof(AlertModal), title, parameters);
    }

    public async Task ShowSuccessAsync(string title, string message, string closeText = "")
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            { "CloseText", string.IsNullOrEmpty(closeText) ? _localizer["OK"] : closeText },
            { "IsSuccess", true }
        };

        await ShowAsync(typeof(AlertModal), title, parameters);
    }

    public async Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string defaultValue = "")
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            { "Placeholder", placeholder },
            { "DefaultValue", defaultValue }
        };

        var result = await ShowAsync(typeof(PromptModal), title, parameters);
        return result.Confirmed ? result.Data?.ToString() : null;
    }

    public void Close(ModalResult result)
    {
        if (_currentModalReference != null)
        {
            _currentModalReference.TaskCompletionSource.SetResult(result);
            _currentModalReference = null;
        }
        OnClose?.Invoke();
    }
}