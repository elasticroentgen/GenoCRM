using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenoCRM.Services.Business;

public class MessagingService : IMessagingService
{
    private readonly GenoDbContext _context;
    private readonly IEmailProvider _emailProvider;
    private readonly IWhatsAppProvider _whatsAppProvider;
    private readonly ISmsProvider _smsProvider;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        GenoDbContext context,
        IEmailProvider emailProvider,
        IWhatsAppProvider whatsAppProvider,
        ISmsProvider smsProvider,
        ILogger<MessagingService> logger)
    {
        _context = context;
        _emailProvider = emailProvider;
        _whatsAppProvider = whatsAppProvider;
        _smsProvider = smsProvider;
        _logger = logger;
    }

    public async Task<Message> SendMessageAsync(int memberId, string subject, string content, 
        MessageType type, MessageChannel channel, int? templateId = null, int? userId = null)
    {
        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
            throw new ArgumentException($"Member with ID {memberId} not found");

        var message = new Message
        {
            MemberId = memberId,
            UserId = userId,
            Subject = subject,
            Content = content,
            Type = type,
            Channel = channel,
            Status = MessageStatus.Pending
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Queue for immediate processing
        await ProcessSingleMessageAsync(message);

        return message;
    }

    public async Task<Message> SendMessageFromTemplateAsync(int memberId, int templateId, 
        Dictionary<string, object>? variables = null, int? userId = null)
    {
        var template = await _context.MessageTemplates.FindAsync(templateId);
        if (template == null)
            throw new ArgumentException($"Template with ID {templateId} not found");

        var member = await _context.Members.FindAsync(memberId);
        if (member == null)
            throw new ArgumentException($"Member with ID {memberId} not found");

        var subject = ProcessTemplate(template.Subject, member, variables);
        var content = ProcessTemplate(template.Content, member, variables);

        return await SendMessageAsync(memberId, subject, content, template.Type, 
            template.Channel, templateId, userId);
    }

    public async Task<MessageCampaign> CreateCampaignAsync(string name, string subject, string content, 
        MessageType type, MessageChannel channel, string? memberFilter = null, 
        DateTime? scheduledAt = null, int? userId = null)
    {
        var campaign = new MessageCampaign
        {
            Name = name,
            Subject = subject,
            Content = content,
            Type = type,
            Channel = channel,
            Status = CampaignStatus.Draft,
            MemberFilter = memberFilter,
            ScheduledAt = scheduledAt
        };

        _context.MessageCampaigns.Add(campaign);
        await _context.SaveChangesAsync();

        return campaign;
    }

    public async Task<MessageCampaign> StartCampaignAsync(int campaignId, int? userId = null)
    {
        var campaign = await _context.MessageCampaigns.FindAsync(campaignId);
        if (campaign == null)
            throw new ArgumentException($"Campaign with ID {campaignId} not found");

        if (campaign.Status != CampaignStatus.Draft && campaign.Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException("Campaign can only be started from Draft or Scheduled status");

        var members = await GetMembersForBroadcastAsync(campaign.MemberFilter);
        campaign.TotalMembers = members.Count();
        campaign.Status = CampaignStatus.Running;
        campaign.StartedAt = DateTime.UtcNow;

        var messages = new List<Message>();
        foreach (var member in members)
        {
            var message = new Message
            {
                MemberId = member.Id,
                UserId = userId,
                Subject = ProcessTemplate(campaign.Subject, member),
                Content = ProcessTemplate(campaign.Content, member),
                Type = campaign.Type,
                Channel = campaign.Channel,
                Status = MessageStatus.Pending
            };

            messages.Add(message);
        }

        _context.Messages.AddRange(messages);
        await _context.SaveChangesAsync();

        // Process messages asynchronously
        _ = Task.Run(async () =>
        {
            foreach (var message in messages)
            {
                await ProcessSingleMessageAsync(message);
                await Task.Delay(1000); // Rate limiting
            }

            // Update campaign status
            campaign.Status = CampaignStatus.Completed;
            campaign.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        });

        return campaign;
    }

    public async Task<MessageCampaign> CancelCampaignAsync(int campaignId, int? userId = null)
    {
        var campaign = await _context.MessageCampaigns.FindAsync(campaignId);
        if (campaign == null)
            throw new ArgumentException($"Campaign with ID {campaignId} not found");

        campaign.Status = CampaignStatus.Cancelled;
        await _context.SaveChangesAsync();

        // Cancel pending messages for this campaign
        var pendingMessages = await _context.Messages
            .Where(m => m.Status == MessageStatus.Pending || m.Status == MessageStatus.Queued)
            .ToListAsync();

        foreach (var message in pendingMessages)
        {
            message.Status = MessageStatus.Cancelled;
        }

        await _context.SaveChangesAsync();

        return campaign;
    }

    public async Task<IEnumerable<Message>> GetMessagesForMemberAsync(int memberId, MessageType? type = null)
    {
        var query = _context.Messages
            .Include(m => m.Member)
            .Include(m => m.User)
            .Where(m => m.MemberId == memberId);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetMessagesByTypeAsync(MessageType type, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Messages
            .Include(m => m.Member)
            .Include(m => m.User)
            .Where(m => m.Type == type);

        if (from.HasValue)
            query = query.Where(m => m.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.CreatedAt <= to.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetPendingMessagesAsync()
    {
        return await _context.Messages
            .Include(m => m.Member)
            .Where(m => m.Status == MessageStatus.Pending || m.Status == MessageStatus.Queued)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetFailedMessagesAsync()
    {
        return await _context.Messages
            .Include(m => m.Member)
            .Where(m => m.Status == MessageStatus.Failed)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RetryMessageAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null || message.Status != MessageStatus.Failed)
            return false;

        message.Status = MessageStatus.Pending;
        message.RetryCount++;
        message.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, message.RetryCount)); // Exponential backoff
        message.ErrorMessage = null;

        await _context.SaveChangesAsync();
        await ProcessSingleMessageAsync(message);

        return true;
    }

    public async Task<bool> CancelMessageAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null || message.Status == MessageStatus.Sent || message.Status == MessageStatus.Delivered)
            return false;

        message.Status = MessageStatus.Cancelled;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<MessageTemplate> CreateTemplateAsync(string name, string subject, string content, 
        MessageType type, MessageChannel channel, string? description = null, string? variables = null)
    {
        var template = new MessageTemplate
        {
            Name = name,
            Subject = subject,
            Content = content,
            Type = type,
            Channel = channel,
            Description = description,
            Variables = variables,
            IsActive = true
        };

        _context.MessageTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task<MessageTemplate> UpdateTemplateAsync(int templateId, string name, string subject, 
        string content, string? description = null, string? variables = null)
    {
        var template = await _context.MessageTemplates.FindAsync(templateId);
        if (template == null)
            throw new ArgumentException($"Template with ID {templateId} not found");

        template.Name = name;
        template.Subject = subject;
        template.Content = content;
        template.Description = description;
        template.Variables = variables;

        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<bool> DeleteTemplateAsync(int templateId)
    {
        var template = await _context.MessageTemplates.FindAsync(templateId);
        if (template == null)
            return false;

        template.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<MessageTemplate>> GetTemplatesAsync(MessageType? type = null, 
        MessageChannel? channel = null, bool? isActive = null)
    {
        var query = _context.MessageTemplates.AsQueryable();

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (channel.HasValue)
            query = query.Where(t => t.Channel == channel.Value);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<MessageTemplate?> GetTemplateAsync(int templateId)
    {
        return await _context.MessageTemplates.FindAsync(templateId);
    }

    public async Task<MessagePreference> SetMemberPreferenceAsync(int memberId, MessageType type, 
        MessageChannel preferredChannel, bool isEnabled = true)
    {
        var existingPreference = await _context.MessagePreferences
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.Type == type);

        if (existingPreference != null)
        {
            existingPreference.PreferredChannel = preferredChannel;
            existingPreference.IsEnabled = isEnabled;
            await _context.SaveChangesAsync();
            return existingPreference;
        }

        var preference = new MessagePreference
        {
            MemberId = memberId,
            Type = type,
            PreferredChannel = preferredChannel,
            IsEnabled = isEnabled
        };

        _context.MessagePreferences.Add(preference);
        await _context.SaveChangesAsync();

        return preference;
    }

    public async Task<MessagePreference?> GetMemberPreferenceAsync(int memberId, MessageType type)
    {
        return await _context.MessagePreferences
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.Type == type);
    }

    public async Task<IEnumerable<MessagePreference>> GetMemberPreferencesAsync(int memberId)
    {
        return await _context.MessagePreferences
            .Where(p => p.MemberId == memberId)
            .ToListAsync();
    }

    public async Task<bool> IsMemberOptedInAsync(int memberId, MessageType type)
    {
        var preference = await GetMemberPreferenceAsync(memberId, type);
        return preference?.IsEnabled ?? true; // Default to opted in
    }

    public async Task<MessageChannel> GetPreferredChannelAsync(int memberId, MessageType type)
    {
        var preference = await GetMemberPreferenceAsync(memberId, type);
        if (preference != null)
            return preference.PreferredChannel;

        // Default channel based on member's available contact methods
        var member = await _context.Members.FindAsync(memberId);
        if (member != null)
        {
            if (!string.IsNullOrEmpty(member.Email))
                return MessageChannel.Email;
            if (!string.IsNullOrEmpty(member.Phone))
                return MessageChannel.SMS;
        }

        return MessageChannel.Email; // Default fallback
    }

    public async Task<IEnumerable<Member>> GetMembersForPaymentRemindersAsync()
    {
        return await _context.Members
            .Include(m => m.Shares)
            .Where(m => m.Status == MemberStatus.Active &&
                       m.Shares.Any(s => s.Status == ShareStatus.Active && !s.IsFullyPaid))
            .ToListAsync();
    }

    public async Task<IEnumerable<Member>> GetMembersForDividendNotificationAsync(int fiscalYear)
    {
        return await _context.Members
            .Include(m => m.Dividends)
            .Where(m => m.Status == MemberStatus.Active &&
                       m.Dividends.Any(d => d.FiscalYear == fiscalYear))
            .ToListAsync();
    }

    public async Task<IEnumerable<Member>> GetMembersForBroadcastAsync(string? memberFilter = null)
    {
        var query = _context.Members
            .Where(m => m.Status == MemberStatus.Active);

        if (!string.IsNullOrEmpty(memberFilter))
        {
            // Parse filter JSON and apply conditions
            // This is a simplified implementation - in production you'd want more robust filtering
            var filterOptions = JsonSerializer.Deserialize<Dictionary<string, object>>(memberFilter);
            
            if (filterOptions != null)
            {
                if (filterOptions.ContainsKey("memberType"))
                {
                    if (Enum.TryParse<MemberType>(filterOptions["memberType"].ToString(), out var memberType))
                        query = query.Where(m => m.MemberType == memberType);
                }
                
                if (filterOptions.ContainsKey("joinedAfter"))
                {
                    if (DateTime.TryParse(filterOptions["joinedAfter"].ToString(), out var joinedAfter))
                        query = query.Where(m => m.JoinDate >= joinedAfter);
                }
            }
        }

        return await query.ToListAsync();
    }

    public async Task ProcessMessageQueueAsync()
    {
        var pendingMessages = await GetPendingMessagesAsync();
        
        foreach (var message in pendingMessages)
        {
            await ProcessSingleMessageAsync(message);
            await Task.Delay(500); // Rate limiting
        }
    }

    public async Task<Dictionary<string, object>> GetMessageStatisticsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Messages.AsQueryable();

        if (from.HasValue)
            query = query.Where(m => m.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.CreatedAt <= to.Value);

        var stats = new Dictionary<string, object>();

        stats["totalMessages"] = await query.CountAsync();
        stats["sentMessages"] = await query.CountAsync(m => m.Status == MessageStatus.Sent);
        stats["deliveredMessages"] = await query.CountAsync(m => m.Status == MessageStatus.Delivered);
        stats["failedMessages"] = await query.CountAsync(m => m.Status == MessageStatus.Failed);
        stats["pendingMessages"] = await query.CountAsync(m => m.Status == MessageStatus.Pending);

        stats["messagesByType"] = await query
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type.ToString(), x => (object)x.Count);

        stats["messagesByChannel"] = await query
            .GroupBy(m => m.Channel)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Channel.ToString(), x => (object)x.Count);

        return stats;
    }

    private async Task ProcessSingleMessageAsync(Message message)
    {
        try
        {
            // Check if member is opted in
            if (!await IsMemberOptedInAsync(message.MemberId, message.Type))
            {
                message.Status = MessageStatus.Cancelled;
                message.ErrorMessage = "Member has opted out of this message type";
                await _context.SaveChangesAsync();
                return;
            }

            message.Status = MessageStatus.Sending;
            await _context.SaveChangesAsync();

            var member = await _context.Members.FindAsync(message.MemberId);
            if (member == null)
            {
                message.Status = MessageStatus.Failed;
                message.ErrorMessage = "Member not found";
                await _context.SaveChangesAsync();
                return;
            }

            bool success = false;
            string? errorMessage = null;

            switch (message.Channel)
            {
                case MessageChannel.Email:
                    (success, errorMessage) = await _emailProvider.SendEmailAsync(
                        member.Email, message.Subject, message.Content);
                    break;
                    
                case MessageChannel.WhatsApp:
                    (success, errorMessage) = await _whatsAppProvider.SendWhatsAppAsync(
                        member.Phone, message.Content);
                    break;
                    
                case MessageChannel.SMS:
                    (success, errorMessage) = await _smsProvider.SendSmsAsync(
                        member.Phone, message.Content);
                    break;
            }

            if (success)
            {
                message.Status = MessageStatus.Sent;
                message.SentAt = DateTime.UtcNow;
            }
            else
            {
                message.Status = MessageStatus.Failed;
                message.ErrorMessage = errorMessage;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            message.Status = MessageStatus.Failed;
            message.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync();
        }
    }

    private string ProcessTemplate(string template, Member member, Dictionary<string, object>? variables = null)
    {
        var result = template;
        
        // Replace member variables
        result = result.Replace("{{Member.FullName}}", member.FullName);
        result = result.Replace("{{Member.FirstName}}", member.FirstName);
        result = result.Replace("{{Member.LastName}}", member.LastName);
        result = result.Replace("{{Member.MemberNumber}}", member.MemberNumber);
        result = result.Replace("{{Member.Email}}", member.Email);
        result = result.Replace("{{Member.Phone}}", member.Phone);
        
        // Replace additional variables
        if (variables != null)
        {
            foreach (var kvp in variables)
            {
                result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
            }
        }
        
        return result;
    }
}