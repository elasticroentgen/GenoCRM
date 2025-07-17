using Microsoft.AspNetCore.Components;

namespace GenoCRM.Services.UI;

public interface IModalService
{
    event Action<ModalReference>? OnShow;
    event Action? OnClose;

    Task<ModalResult> ShowAsync<T>(string title, Dictionary<string, object>? parameters = null) where T : ComponentBase;
    Task<ModalResult> ShowAsync(Type componentType, string title, Dictionary<string, object>? parameters = null);
    
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");
    Task ShowAlertAsync(string title, string message, string closeText = "OK");
    Task ShowErrorAsync(string title, string message, string closeText = "OK");
    Task ShowSuccessAsync(string title, string message, string closeText = "OK");
    Task<string?> ShowPromptAsync(string title, string message, string placeholder = "", string defaultValue = "");
    
    void Close(ModalResult result);
}

public class ModalReference
{
    public Type ComponentType { get; set; } = typeof(object);
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TaskCompletionSource<ModalResult> TaskCompletionSource { get; set; } = new();
}

public class ModalResult
{
    public bool Confirmed { get; set; }
    public object? Data { get; set; }
    
    public static ModalResult Ok() => new() { Confirmed = true };
    public static ModalResult Ok(object data) => new() { Confirmed = true, Data = data };
    public static ModalResult Cancel() => new() { Confirmed = false };
}