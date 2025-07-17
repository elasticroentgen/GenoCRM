using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GenoCRM.Services.Business.Messaging;

public class SmsProvider : ISmsProvider
{
    private readonly SmsSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmsProvider> _logger;

    public SmsProvider(IOptions<SmsSettings> settings, HttpClient httpClient, 
        ILogger<SmsProvider> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool success, string? errorMessage)> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            if (!await ValidatePhoneNumberAsync(phoneNumber))
            {
                return (false, "Invalid phone number format");
            }

            var payload = new
            {
                from = _settings.FromNumber,
                to = FormatPhoneNumber(phoneNumber),
                text = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add authentication headers based on provider
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            }

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SMS sent successfully to {PhoneNumber}", phoneNumber);
                return (true, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SMS API error: {StatusCode} - {Content}", 
                    response.StatusCode, errorContent);
                return (false, $"SMS API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
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
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            }

            var response = await _httpClient.GetAsync($"{_settings.StatusUrl}/{messageId}");

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
            _logger.LogError(ex, "Failed to get delivery status for SMS {MessageId}", messageId);
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "error", ex.Message }
            };
        }
    }

    public async Task<decimal> GetSmsRateAsync(string phoneNumber)
    {
        // This would typically call the SMS provider's pricing API
        // For now, return a default rate
        return await Task.FromResult(_settings.DefaultRate);
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

public class SmsSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string StatusUrl { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; } = 0.05m;
    public string Provider { get; set; } = "generic"; // twilio, nexmo, etc.
}