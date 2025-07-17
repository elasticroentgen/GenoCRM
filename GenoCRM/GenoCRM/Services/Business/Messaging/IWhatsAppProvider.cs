namespace GenoCRM.Services.Business.Messaging;

public interface IWhatsAppProvider
{
    Task<(bool success, string? errorMessage)> SendWhatsAppAsync(string phoneNumber, string message);
    
    Task<(bool success, string? errorMessage)> SendWhatsAppTemplateAsync(string phoneNumber, 
        string templateName, Dictionary<string, object> parameters);
    
    Task<bool> ValidatePhoneNumberAsync(string phoneNumber);
    
    Task<Dictionary<string, object>> GetDeliveryStatusAsync(string messageId);
}