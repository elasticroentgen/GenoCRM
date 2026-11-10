using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

/// <summary>Texts and links used in the payout notification mail sent with a SEPA export.</summary>
public class LoanPayoutNotificationSettings
{
    /// <summary>Name of the cooperative used in the mail text and signature; falls back to the SEPA debtor name.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Address members can contact with questions (e.g. changed bank details).</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Link to the privacy policy (Art. 13 GDPR information).</summary>
    public string PrivacyPolicyUrl { get; set; } = string.Empty;

    /// <summary>Tax note shown in the mail. Have it checked by your tax advisor; empty = no tax note.</summary>
    public string TaxNotice { get; set; } =
        "Die Zinsen werden ohne Steuerabzug ausgezahlt. Bitte geben Sie sie in Ihrer Einkommensteuererklärung an.";
}

/// <summary>One mail to one member, covering all of the member's payments in the export.</summary>
public record LoanPayoutNotification(
    Member Member,
    string Subject,
    string HtmlBody,
    IReadOnlyList<LoanPaymentPlanEntry> Entries)
{
    public bool HasEmail => !string.IsNullOrWhiteSpace(Member.Email);
}

public record LoanPayoutNotificationResult(
    int Sent,
    IReadOnlyList<string> Failed,
    IReadOnlyList<string> OptedOut,
    IReadOnlyList<string> WithoutEmail);

public interface ILoanPayoutNotificationService
{
    /// <summary>Builds one notification per member. Nothing is sent or stored.</summary>
    IReadOnlyList<LoanPayoutNotification> BuildNotifications(
        IEnumerable<LoanPaymentPlanEntry> entries, Func<LoanPaymentPlanEntry, DateTime> executionDate);

    /// <summary>Sends the notifications by e-mail; every mail is recorded as a <see cref="Message"/>.</summary>
    Task<LoanPayoutNotificationResult> SendAsync(IReadOnlyList<LoanPayoutNotification> notifications, int? userId = null);
}

public class LoanPayoutNotificationService : ILoanPayoutNotificationService
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    // Escapes markup characters but keeps umlauts readable in the stored message.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly IMessagingService _messagingService;
    private readonly ILogger<LoanPayoutNotificationService> _logger;
    private readonly LoanPayoutNotificationSettings _settings;

    public LoanPayoutNotificationService(IMessagingService messagingService, IConfiguration configuration,
        ILogger<LoanPayoutNotificationService> logger)
    {
        _messagingService = messagingService;
        _logger = logger;
        _settings = configuration.GetSection("LoanPayoutNotification").Get<LoanPayoutNotificationSettings>()
                    ?? new LoanPayoutNotificationSettings();
        if (string.IsNullOrWhiteSpace(_settings.OrganizationName))
            _settings.OrganizationName = configuration["SepaDebtor:Name"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_settings.ContactEmail))
            _settings.ContactEmail = configuration["Smtp:FromEmail"] ?? string.Empty;
    }

    public IReadOnlyList<LoanPayoutNotification> BuildNotifications(
        IEnumerable<LoanPaymentPlanEntry> entries, Func<LoanPaymentPlanEntry, DateTime> executionDate)
    {
        return entries
            .GroupBy(e => e.LoanPaymentPlan.LoanSubscription.Member)
            .OrderBy(g => g.Key.LastName).ThenBy(g => g.Key.FirstName)
            .Select(g =>
            {
                var memberEntries = g
                    .OrderBy(e => e.LoanPaymentPlan.LoanSubscription.SubscriptionNumber)
                    .ThenBy(e => e.DueDate)
                    .ToList();
                return new LoanPayoutNotification(g.Key, BuildSubject(memberEntries),
                    BuildBody(g.Key, memberEntries, executionDate, _settings), memberEntries);
            })
            .ToList();
    }

    public async Task<LoanPayoutNotificationResult> SendAsync(IReadOnlyList<LoanPayoutNotification> notifications, int? userId = null)
    {
        var sent = 0;
        var failed = new List<string>();
        var optedOut = new List<string>();
        var withoutEmail = new List<string>();

        foreach (var notification in notifications)
        {
            var name = notification.Member.FullName;
            if (!notification.HasEmail)
            {
                withoutEmail.Add(name);
                continue;
            }

            try
            {
                var message = await _messagingService.SendMessageAsync(notification.Member.Id, notification.Subject,
                    notification.HtmlBody, MessageType.LoanPayoutNotification, MessageChannel.Email, userId: userId);
                switch (message.Status)
                {
                    case MessageStatus.Sent:
                    case MessageStatus.Delivered:
                        sent++;
                        break;
                    case MessageStatus.Cancelled:
                        optedOut.Add(name);
                        break;
                    default:
                        failed.Add($"{name}: {message.ErrorMessage}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payout notification to member {MemberId}", notification.Member.Id);
                failed.Add($"{name}: {ex.Message}");
            }
        }

        _logger.LogInformation("Payout notifications: {Sent} sent, {Failed} failed, {OptedOut} opted out, {NoEmail} without e-mail",
            sent, failed.Count, optedOut.Count, withoutEmail.Count);
        return new LoanPayoutNotificationResult(sent, failed, optedOut, withoutEmail);
    }

    private static string BuildSubject(IReadOnlyList<LoanPaymentPlanEntry> entries)
    {
        var numbers = entries.Select(e => e.LoanPaymentPlan.LoanSubscription.SubscriptionNumber).Distinct().ToList();
        return numbers.Count == 1
            ? $"Anstehende Auszahlung zu Ihrem Darlehen {numbers[0]}"
            : "Anstehende Auszahlungen zu Ihren Darlehen";
    }

    /// <summary>
    /// Builds the mail body. Deliberately contains only what the member needs to check the payment:
    /// no address, birth date or full bank details (IBAN is masked), no gendered salutation.
    /// </summary>
    public static string BuildBody(Member member, IReadOnlyList<LoanPaymentPlanEntry> entries,
        Func<LoanPaymentPlanEntry, DateTime> executionDate, LoanPayoutNotificationSettings settings)
    {
        var org = settings.OrganizationName;
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: Arial, Helvetica, sans-serif; font-size: 14px; color: #222; max-width: 640px;\">");
        sb.Append($"<p>Guten Tag {H(member.FullName)},</p>");
        sb.Append(entries.Count == 1
            ? $"<p>für Ihr Darlehen{(string.IsNullOrWhiteSpace(org) ? "" : $" an die {H(org)}")} steht folgende Auszahlung an:</p>"
            : $"<p>für Ihre Darlehen{(string.IsNullOrWhiteSpace(org) ? "" : $" an die {H(org)}")} stehen folgende Auszahlungen an:</p>");

        foreach (var entry in entries)
        {
            var sub = entry.LoanPaymentPlan.LoanSubscription;
            var offer = sub.LoanOffer;
            sb.Append("<table style=\"border-collapse: collapse; margin: 0 0 16px 0; min-width: 360px;\">");
            sb.Append($"<tr><th colspan=\"2\" style=\"text-align: left; padding: 6px 0; border-bottom: 1px solid #ccc;\">" +
                      $"Darlehen {H(sub.SubscriptionNumber)} – {H(offer.LoanProject.Title)}</th></tr>");
            Row(sb, "Darlehensbetrag", Money(sub.Amount));
            Row(sb, "Zinssatz", $"{(offer.InterestRate * 100).ToString("0.00", De)} % p. a.");
            if (entry.InterestPeriodStart is { } start && entry.InterestPeriodEnd is { } end && entry.InterestAmount > 0)
            {
                var days = entry.InterestDays is { } d ? $" ({d} Tage)" : "";
                Row(sb, "Zinszeitraum", $"{Date(start)} – {Date(end.AddDays(-1))}{days}");
            }
            Row(sb, "Zinsen", Money(entry.InterestAmount));
            Row(sb, "Tilgung", Money(entry.PrincipalAmount));
            Row(sb, "Auszahlungsbetrag", Money(entry.TotalAmount), bold: true);
            Row(sb, "Restschuld nach Auszahlung", Money(entry.RemainingBalance));
            Row(sb, "Voraussichtliche Ausführung", Date(executionDate(entry)));
            Row(sb, "Empfängerkonto", MaskIban(sub.IBAN));
            sb.Append("</table>");
        }

        sb.Append("<p>Die Überweisung erfolgt auf das oben angegebene Konto. Sollte sich Ihre Bankverbindung geändert haben, " +
                  "teilen Sie uns dies bitte umgehend mit.</p>");
        if (!string.IsNullOrWhiteSpace(settings.TaxNotice))
            sb.Append($"<p>{H(settings.TaxNotice)}</p>");
        sb.Append(string.IsNullOrWhiteSpace(settings.ContactEmail)
            ? "<p>Bei Fragen melden Sie sich gerne bei uns.</p>"
            : $"<p>Bei Fragen erreichen Sie uns unter <a href=\"mailto:{H(settings.ContactEmail)}\">{H(settings.ContactEmail)}</a>.</p>");
        sb.Append($"<p>Mit freundlichen Grüßen<br />{(string.IsNullOrWhiteSpace(org) ? "Ihre Genossenschaft" : H(org))}</p>");

        if (!string.IsNullOrWhiteSpace(settings.PrivacyPolicyUrl))
        {
            sb.Append("<p style=\"font-size: 12px; color: #666; border-top: 1px solid #eee; padding-top: 8px;\">" +
                      "Informationen zur Verarbeitung Ihrer personenbezogenen Daten finden Sie in unserer " +
                      $"<a href=\"{H(settings.PrivacyPolicyUrl)}\">Datenschutzerklärung</a>.</p>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>Masks an IBAN except country code and the last four digits, e.g. "DE** **** **** **** **30 00".</summary>
    public static string MaskIban(string iban)
    {
        var normalized = SepaExportService.NormalizeIban(iban);
        if (normalized.Length <= 6) return new string('*', normalized.Length);
        var masked = normalized[..2] + new string('*', normalized.Length - 6) + normalized[^4..];
        return string.Join(' ', masked.Chunk(4).Select(c => new string(c)));
    }

    private static void Row(StringBuilder sb, string label, string value, bool bold = false)
    {
        var weight = bold ? "font-weight: bold; " : "";
        sb.Append($"<tr><td style=\"padding: 3px 16px 3px 0; color: #555;\">{H(label)}</td>" +
                  $"<td style=\"{weight}padding: 3px 0; text-align: right;\">{H(value)}</td></tr>");
    }

    private static string Money(decimal amount) => amount.ToString("N2", De) + " €";
    private static string Date(DateTime date) => date.ToString("dd.MM.yyyy", De);
    private static string H(string? value) => Html.Encode(value ?? string.Empty);
}
