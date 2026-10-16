using System.Text;
using System.Text.Json;
using GenoCRM.Models.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GenoCRM.Services.Integration;

public class ListmonkService : IListmonkService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ListmonkService> _logger;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _newsletterListName;
    private readonly bool _enabled;
    private int? _cachedListId;

    public ListmonkService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ListmonkService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _baseUrl = configuration["Listmonk:BaseUrl"] ?? "";
        _username = configuration["Listmonk:Username"] ?? "";
        _password = configuration["Listmonk:Password"] ?? "";
        _newsletterListName = configuration["Listmonk:NewsletterListName"] ?? "BEW Newsletter - Mitglieder";
        _enabled = configuration.GetValue<bool>("Listmonk:Enabled");

        if (_enabled && !string.IsNullOrEmpty(_baseUrl) && !string.IsNullOrEmpty(_username))
        {
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
            _httpClient.BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/api/");
        }
    }

    public async Task SyncMemberAsync(Member member)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Listmonk integration is disabled, skipping sync for member {MemberId}", member.Id);
            return;
        }

        if (string.IsNullOrEmpty(member.Email))
        {
            _logger.LogWarning("Member {MemberId} has no email address, skipping Listmonk sync", member.Id);
            return;
        }

        try
        {
            if (member.Status == MemberStatus.Active)
            {
                // Create or update subscriber and subscribe to list
                await CreateOrUpdateSubscriberAsync(member);
                _logger.LogInformation("Synced active member {MemberId} to Listmonk", member.Id);
            }
            else
            {
                // Unsubscribe from list
                await UnsubscribeMemberAsync(member);
                _logger.LogInformation("Unsubscribed member {MemberId} from Listmonk newsletter", member.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing member {MemberId} to Listmonk", member.Id);
            // Don't throw - we don't want to fail the member operation if Listmonk sync fails
        }
    }

    public async Task<int?> CreateOrUpdateSubscriberAsync(Member member)
    {
        if (!_enabled) return null;

        try
        {
            var listId = await GetNewsletterListIdAsync();
            if (!listId.HasValue)
            {
                _logger.LogWarning("Newsletter list '{ListName}' not found in Listmonk", _newsletterListName);
                return null;
            }

            // Check if subscriber exists
            var existingSubscriber = await GetSubscriberByEmailAsync(member.Email);

            if (existingSubscriber != null && existingSubscriber.Status == "blocklisted")
            {
                // The person opted out in Listmonk; never silently re-enable them
                _logger.LogInformation("Subscriber {SubscriberId} for member {MemberId} is blocklisted, leaving untouched",
                    existingSubscriber.Id, member.Id);
                return existingSubscriber.Id;
            }

            // Keep memberships in other lists when updating
            var lists = existingSubscriber == null
                ? new[] { listId.Value }
                : existingSubscriber.Lists.Append(listId.Value).Distinct().ToArray();

            var subscriberData = new
            {
                email = member.Email,
                name = member.FullName,
                status = "enabled",
                lists,
                attribs = new
                {
                    member_number = member.MemberNumber,
                    member_type = member.MemberType.ToString(),
                    join_date = member.JoinDate.ToString("yyyy-MM-dd")
                }
            };

            HttpResponseMessage response;
            if (existingSubscriber != null)
            {
                // Update existing subscriber
                var json = JsonSerializer.Serialize(subscriberData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PutAsync($"subscribers/{existingSubscriber.Id}", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Updated subscriber {SubscriberId} for member {MemberId}",
                        existingSubscriber.Id, member.Id);
                    return existingSubscriber.Id;
                }
            }
            else
            {
                // Create new subscriber
                var json = JsonSerializer.Serialize(subscriberData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync("subscribers", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (result.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("id", out var idElement))
                    {
                        var subscriberId = idElement.GetInt32();
                        _logger.LogInformation("Created subscriber {SubscriberId} for member {MemberId}",
                            subscriberId, member.Id);
                        return subscriberId;
                    }
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create/update subscriber for member {MemberId}: {StatusCode} - {Error}",
                member.Id, response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating subscriber for member {MemberId}", member.Id);
            return null;
        }
    }

    public async Task UnsubscribeMemberAsync(Member member)
    {
        if (!_enabled) return;

        try
        {
            var subscriber = await GetSubscriberByEmailAsync(member.Email);
            if (subscriber == null)
            {
                _logger.LogDebug("Subscriber not found for member {MemberId}, nothing to unsubscribe", member.Id);
                return;
            }

            var listId = await GetNewsletterListIdAsync();
            if (!listId.HasValue)
            {
                _logger.LogWarning("Newsletter list '{ListName}' not found in Listmonk", _newsletterListName);
                return;
            }

            // Unsubscribe from the newsletter list only, keeping the subscriber record and other lists.
            // PUT /api/subscribers/lists is Listmonk's list-membership endpoint; the subscriber
            // update endpoint would require the full record and does not accept "disabled".
            var updateData = new
            {
                ids = new[] { subscriber.Id },
                action = "unsubscribe",
                target_list_ids = new[] { listId.Value }
            };

            var json = JsonSerializer.Serialize(updateData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync("subscribers/lists", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Unsubscribed member {MemberId} from newsletter list", member.Id);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to unsubscribe member {MemberId}: {StatusCode} - {Error}",
                    member.Id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing member {MemberId}", member.Id);
        }
    }

    public async Task DeleteSubscriberAsync(int subscriberId)
    {
        if (!_enabled) return;

        try
        {
            var response = await _httpClient.DeleteAsync($"subscribers/{subscriberId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted subscriber {SubscriberId} from Listmonk", subscriberId);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to delete subscriber {SubscriberId}: {StatusCode} - {Error}",
                    subscriberId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscriber {SubscriberId}", subscriberId);
        }
    }

    public async Task<ListmonkSubscriber?> GetSubscriberByEmailAsync(string email)
    {
        if (!_enabled) return null;

        try
        {
            // The query is evaluated as SQL by Listmonk: escape quotes, then URL-encode so '+' survives
            var escapedEmail = email.Replace("'", "''");
            var query = Uri.EscapeDataString($"subscribers.email='{escapedEmail}'");
            var response = await _httpClient.GetAsync($"subscribers?query={query}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            if (result.TryGetProperty("data", out var data) &&
                data.TryGetProperty("results", out var results) &&
                results.GetArrayLength() > 0)
            {
                var subscriber = results[0];
                return new ListmonkSubscriber
                {
                    Id = subscriber.GetProperty("id").GetInt32(),
                    Email = subscriber.GetProperty("email").GetString() ?? "",
                    Name = subscriber.GetProperty("name").GetString() ?? "",
                    Status = subscriber.GetProperty("status").GetString() ?? "",
                    Lists = subscriber.GetProperty("lists").EnumerateArray()
                        .Select(l => l.GetProperty("id").GetInt32()).ToList(),
                    Attribs = subscriber.TryGetProperty("attribs", out var attribs)
                        ? JsonSerializer.Deserialize<Dictionary<string, object>>(attribs.GetRawText()) ?? new()
                        : new()
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscriber by email {Email}", email);
            return null;
        }
    }

    public async Task<int?> GetNewsletterListIdAsync()
    {
        if (!_enabled) return null;

        // Return cached list ID if available
        if (_cachedListId.HasValue)
        {
            return _cachedListId;
        }

        try
        {
            var response = await _httpClient.GetAsync("lists");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch lists from Listmonk: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            if (result.TryGetProperty("data", out var data) &&
                data.TryGetProperty("results", out var results))
            {
                foreach (var list in results.EnumerateArray())
                {
                    var name = list.GetProperty("name").GetString();
                    if (name == _newsletterListName)
                    {
                        _cachedListId = list.GetProperty("id").GetInt32();
                        _logger.LogInformation("Found newsletter list '{ListName}' with ID {ListId}",
                            _newsletterListName, _cachedListId);
                        return _cachedListId;
                    }
                }
            }

            _logger.LogWarning("Newsletter list '{ListName}' not found in Listmonk", _newsletterListName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting newsletter list ID");
            return null;
        }
    }
}
