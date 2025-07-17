namespace GenoCRM.Services.Business.Messaging;

public interface IEmailProvider
{
    Task<(bool success, string? errorMessage)> SendEmailAsync(string to, string subject, string body, 
        string? fromEmail = null, string? fromName = null);
    
    Task<(bool success, string? errorMessage)> SendEmailAsync(IEnumerable<string> to, string subject, 
        string body, string? fromEmail = null, string? fromName = null);
    
    Task<bool> ValidateEmailAsync(string email);
    
    Task<Dictionary<string, object>> GetDeliveryStatusAsync(string messageId);
}