using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IMessagingService
{
    Task<Message> SendMessageAsync(int memberId, string subject, string content, MessageType type, 
        MessageChannel channel, int? templateId = null, int? userId = null);
    
    Task<Message> SendMessageFromTemplateAsync(int memberId, int templateId, 
        Dictionary<string, object>? variables = null, int? userId = null);
    
    Task<MessageCampaign> CreateCampaignAsync(string name, string subject, string content, 
        MessageType type, MessageChannel channel, string? memberFilter = null, 
        DateTime? scheduledAt = null, int? userId = null);
    
    Task<MessageCampaign> StartCampaignAsync(int campaignId, int? userId = null);
    
    Task<MessageCampaign> CancelCampaignAsync(int campaignId, int? userId = null);
    
    Task<IEnumerable<Message>> GetMessagesForMemberAsync(int memberId, MessageType? type = null);
    
    Task<IEnumerable<Message>> GetMessagesByTypeAsync(MessageType type, DateTime? from = null, DateTime? to = null);
    
    Task<IEnumerable<Message>> GetPendingMessagesAsync();
    
    Task<IEnumerable<Message>> GetFailedMessagesAsync();
    
    Task<bool> RetryMessageAsync(int messageId);
    
    Task<bool> CancelMessageAsync(int messageId);
    
    Task<MessageTemplate> CreateTemplateAsync(string name, string subject, string content, 
        MessageType type, MessageChannel channel, string? description = null, 
        string? variables = null);
    
    Task<MessageTemplate> UpdateTemplateAsync(int templateId, string name, string subject, 
        string content, string? description = null, string? variables = null);
    
    Task<bool> DeleteTemplateAsync(int templateId);
    
    Task<IEnumerable<MessageTemplate>> GetTemplatesAsync(MessageType? type = null, 
        MessageChannel? channel = null, bool? isActive = null);
    
    Task<MessageTemplate?> GetTemplateAsync(int templateId);
    
    Task<MessagePreference> SetMemberPreferenceAsync(int memberId, MessageType type, 
        MessageChannel preferredChannel, bool isEnabled = true);
    
    Task<MessagePreference?> GetMemberPreferenceAsync(int memberId, MessageType type);
    
    Task<IEnumerable<MessagePreference>> GetMemberPreferencesAsync(int memberId);
    
    Task<bool> IsMemberOptedInAsync(int memberId, MessageType type);
    
    Task<MessageChannel> GetPreferredChannelAsync(int memberId, MessageType type);
    
    Task<IEnumerable<Member>> GetMembersForPaymentRemindersAsync();
    
    Task<IEnumerable<Member>> GetMembersForDividendNotificationAsync(int fiscalYear);
    
    Task<IEnumerable<Member>> GetMembersForBroadcastAsync(string? memberFilter = null);
    
    Task ProcessMessageQueueAsync();
    
    Task<Dictionary<string, object>> GetMessageStatisticsAsync(DateTime? from = null, DateTime? to = null);
}