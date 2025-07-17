namespace GenoCRM.Services.Business.Messaging;

public interface ISmsProvider
{
    Task<(bool success, string? errorMessage)> SendSmsAsync(string phoneNumber, string message);
    
    Task<bool> ValidatePhoneNumberAsync(string phoneNumber);
    
    Task<Dictionary<string, object>> GetDeliveryStatusAsync(string messageId);
    
    Task<decimal> GetSmsRateAsync(string phoneNumber);
}