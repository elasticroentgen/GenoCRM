using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GenoCRM.Services.Business.Messaging;

public class WhatsAppProvider : IWhatsAppProvider
{
    private readonly WhatsAppSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppProvider> _logger;

    public WhatsAppProvider(IOptions<WhatsAppSettings> settings, HttpClient httpClient, 
        ILogger<WhatsAppProvider> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool success, string? errorMessage)> SendWhatsAppAsync(string phoneNumber, string message)
    {
        try
        {
            if (!await ValidatePhoneNumberAsync(phoneNumber))
            {
                return (false, "Invalid phone number format");
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = FormatPhoneNumber(phoneNumber),
                type = "text",
                text = new { body = message }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.AccessToken}");

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("WhatsApp message sent successfully to {PhoneNumber}", phoneNumber);
                return (true, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("WhatsApp API error: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
                return (false, $"WhatsApp API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp message to {PhoneNumber}", phoneNumber);
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? errorMessage)> SendWhatsAppTemplateAsync(string phoneNumber, 
        string templateName, Dictionary<string, object> parameters)
    {
        try
        {
            if (!await ValidatePhoneNumberAsync(phoneNumber))
            {
                return (false, "Invalid phone number format");
            }

            var templateParams = parameters.Select(kvp => new { 
                type = "text", 
                text = kvp.Value?.ToString() 
            }).ToArray();

            var payload = new
            {
                messaging_product = "whatsapp",
                to = FormatPhoneNumber(phoneNumber),
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = "en" },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = templateParams
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.AccessToken}");

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("WhatsApp template message sent successfully to {PhoneNumber}", phoneNumber);
                return (true, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("WhatsApp template API error: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
                return (false, $"WhatsApp template API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp template message to {PhoneNumber}", phoneNumber);
            return (false, ex.Message);
        }
    }

    public async Task<bool> ValidatePhoneNumberAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Basic international phone number validation
        var phoneRegex = new Regex(@"^\+?[1-9]\d{1,14}$");
        return await Task.FromResult(phoneRegex.IsMatch(phoneNumber.Replace(" ", "").Replace("-", "")));
    }

    public async Task<Dictionary<string, object>> GetDeliveryStatusAsync(string messageId)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.AccessToken}");

            var response = await _httpClient.GetAsync($"{_settings.ApiUrl}/{messageId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var statusData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                return statusData ?? new Dictionary<string, object>();
            }
            else
            {
                return new Dictionary<string, object>
                {
                    { "status", "unknown" },
                    { "error", $"API error: {response.StatusCode}" }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get delivery status for message {MessageId}", messageId);
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "error", ex.Message }
            };
        }
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        var cleaned = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        
        if (!cleaned.StartsWith("+"))
        {
            // Assume German country code if no country code provided
            cleaned = "+49" + cleaned.TrimStart('0');
        }
        
        return cleaned;
    }
}

public class WhatsAppSettings
{
    public string AccessToken { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://graph.facebook.com/v18.0/YOUR_PHONE_NUMBER_ID/messages";
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WebhookVerifyToken { get; set; } = string.Empty;
}