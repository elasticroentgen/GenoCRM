using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GenoCRM.Services.Business.Messaging;

public class SmtpEmailProvider : IEmailProvider
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(IOptions<SmtpSettings> settings, ILogger<SmtpEmailProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(bool success, string? errorMessage)> SendEmailAsync(string to, string subject, 
        string body, string? fromEmail = null, string? fromName = null)
    {
        return await SendEmailAsync(new[] { to }, subject, body, fromEmail, fromName);
    }

    public async Task<(bool success, string? errorMessage)> SendEmailAsync(IEnumerable<string> to, 
        string subject, string body, string? fromEmail = null, string? fromName = null)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            var from = new MailAddress(fromEmail ?? _settings.FromEmail, 
                fromName ?? _settings.FromName);

            using var message = new MailMessage
            {
                From = from,
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            foreach (var recipient in to)
            {
                if (await ValidateEmailAsync(recipient))
                {
                    message.To.Add(recipient);
                }
                else
                {
                    _logger.LogWarning("Invalid email address: {Email}", recipient);
                }
            }

            if (message.To.Count == 0)
            {
                return (false, "No valid recipients");
            }

            await client.SendMailAsync(message);
            
            _logger.LogInformation("Email sent successfully to {Recipients}", 
                string.Join(", ", message.To.Select(t => t.Address)));
            
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", to));
            return (false, ex.Message);
        }
    }

    public async Task<bool> ValidateEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return await Task.FromResult(emailRegex.IsMatch(email));
    }

    public async Task<Dictionary<string, object>> GetDeliveryStatusAsync(string messageId)
    {
        // Basic SMTP doesn't provide delivery status tracking
        // This would need integration with email service providers like SendGrid, AWS SES, etc.
        return await Task.FromResult(new Dictionary<string, object>
        {
            { "status", "sent" },
            { "messageId", messageId },
            { "provider", "smtp" }
        });
    }
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}