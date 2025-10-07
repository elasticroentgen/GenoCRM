using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Integration;

public interface IListmonkService
{
    /// <summary>
    /// Syncs a member to Listmonk based on their status
    /// </summary>
    Task SyncMemberAsync(Member member);

    /// <summary>
    /// Creates or updates a subscriber in Listmonk and subscribes to the configured list
    /// </summary>
    Task<int?> CreateOrUpdateSubscriberAsync(Member member);

    /// <summary>
    /// Unsubscribes a member from the configured list
    /// </summary>
    Task UnsubscribeMemberAsync(Member member);

    /// <summary>
    /// Deletes a subscriber from Listmonk
    /// </summary>
    Task DeleteSubscriberAsync(int subscriberId);

    /// <summary>
    /// Gets a subscriber by email
    /// </summary>
    Task<ListmonkSubscriber?> GetSubscriberByEmailAsync(string email);

    /// <summary>
    /// Gets the list ID for the newsletter list
    /// </summary>
    Task<int?> GetNewsletterListIdAsync();
}

public class ListmonkSubscriber
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<int> Lists { get; set; } = new();
    public Dictionary<string, object> Attribs { get; set; } = new();
}

public class ListmonkList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
