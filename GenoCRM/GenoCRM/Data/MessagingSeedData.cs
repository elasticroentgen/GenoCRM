using GenoCRM.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenoCRM.Data;

public static class MessagingSeedData
{
    public static async Task SeedAsync(GenoDbContext context)
    {
        // Check if templates already exist
        if (await context.MessageTemplates.AnyAsync())
            return;

        var templates = new List<MessageTemplate>
        {
            // Payment reminder templates
            new MessageTemplate
            {
                Name = "Payment Reminder - Email",
                Subject = "Zahlungserinnerung für Genossenschaftsanteile - Payment Reminder for Cooperative Shares",
                Content = @"
Liebe/r {{Member.FullName}},

hiermit möchten wir Sie daran erinnern, dass noch eine offene Zahlung für Ihre Genossenschaftsanteile besteht.

Mitgliedsnummer: {{Member.MemberNumber}}
Offener Betrag: {{OutstandingAmount}}
Fälligkeitsdatum: {{DueDate}}

Bitte überweisen Sie den Betrag auf das Konto der Genossenschaft:
[Kontodaten einfügen]

Bei Fragen stehen wir Ihnen gerne zur Verfügung.

Mit freundlichen Grüßen
Ihre Genossenschaft

---

Dear {{Member.FullName}},

This is a reminder that you have an outstanding payment for your cooperative shares.

Member Number: {{Member.MemberNumber}}
Outstanding Amount: {{OutstandingAmount}}
Due Date: {{DueDate}}

Please transfer the amount to the cooperative's account:
[Insert account details]

If you have any questions, please don't hesitate to contact us.

Best regards,
Your Cooperative",
                Type = MessageType.PaymentReminder,
                Channel = MessageChannel.Email,
                Description = "Email template for payment reminders",
                Variables = @"{""OutstandingAmount"": ""decimal"", ""DueDate"": ""date""}",
                IsActive = true
            },

            new MessageTemplate
            {
                Name = "Payment Reminder - WhatsApp",
                Subject = "Zahlungserinnerung",
                Content = @"Hallo {{Member.FirstName}},

Erinnerung: Offene Zahlung für Genossenschaftsanteile
Betrag: {{OutstandingAmount}}
Mitgliedsnummer: {{Member.MemberNumber}}

Bitte überweisen Sie den Betrag zeitnah.

Bei Fragen: [Kontakt]

Ihre Genossenschaft",
                Type = MessageType.PaymentReminder,
                Channel = MessageChannel.WhatsApp,
                Description = "WhatsApp template for payment reminders",
                Variables = @"{""OutstandingAmount"": ""decimal""}",
                IsActive = true
            },

            // Welcome message templates
            new MessageTemplate
            {
                Name = "Welcome Message - Email",
                Subject = "Willkommen in unserer Genossenschaft - Welcome to our Cooperative",
                Content = @"
Liebe/r {{Member.FullName}},

herzlich willkommen als neues Mitglied in unserer Genossenschaft!

Ihre Mitgliedsnummer: {{Member.MemberNumber}}
Beitrittsdatum: {{Member.JoinDate}}

Wir freuen uns, Sie als Teil unserer Gemeinschaft begrüßen zu dürfen. In den nächsten Tagen erhalten Sie weitere Informationen zu Ihren Rechten und Pflichten als Genossenschaftsmitglied.

Bei Fragen stehen wir Ihnen gerne zur Verfügung.

Mit freundlichen Grüßen
Ihre Genossenschaft

---

Dear {{Member.FullName}},

Welcome as a new member to our cooperative!

Your Member Number: {{Member.MemberNumber}}
Join Date: {{Member.JoinDate}}

We are pleased to welcome you as part of our community. In the coming days, you will receive further information about your rights and obligations as a cooperative member.

If you have any questions, please don't hesitate to contact us.

Best regards,
Your Cooperative",
                Type = MessageType.WelcomeMessage,
                Channel = MessageChannel.Email,
                Description = "Email template for welcoming new members",
                Variables = @"{}",
                IsActive = true
            },

            // Dividend notification templates
            new MessageTemplate
            {
                Name = "Dividend Notification - Email",
                Subject = "Dividendenausschüttung {{FiscalYear}} - Dividend Distribution {{FiscalYear}}",
                Content = @"
Liebe/r {{Member.FullName}},

wir freuen uns, Ihnen mitteilen zu können, dass für das Geschäftsjahr {{FiscalYear}} eine Dividende ausgeschüttet wird.

Mitgliedsnummer: {{Member.MemberNumber}}
Dividende pro Anteil: {{DividendPerShare}}
Ihre Anteile: {{ShareCount}}
Ihre Dividende: {{TotalDividend}}

Die Dividende wird in den nächsten Tagen auf Ihr Konto überwiesen.

Mit freundlichen Grüßen
Ihre Genossenschaft

---

Dear {{Member.FullName}},

We are pleased to inform you that a dividend will be distributed for the fiscal year {{FiscalYear}}.

Member Number: {{Member.MemberNumber}}
Dividend per Share: {{DividendPerShare}}
Your Shares: {{ShareCount}}
Your Dividend: {{TotalDividend}}

The dividend will be transferred to your account in the coming days.

Best regards,
Your Cooperative",
                Type = MessageType.DividendNotification,
                Channel = MessageChannel.Email,
                Description = "Email template for dividend notifications",
                Variables = @"{""FiscalYear"": ""number"", ""DividendPerShare"": ""decimal"", ""ShareCount"": ""number"", ""TotalDividend"": ""decimal""}",
                IsActive = true
            },

            // General Assembly notice templates
            new MessageTemplate
            {
                Name = "General Assembly Notice - Email",
                Subject = "Einladung zur Generalversammlung - Invitation to General Assembly",
                Content = @"
Liebe/r {{Member.FullName}},

hiermit laden wir Sie herzlich zur ordentlichen Generalversammlung unserer Genossenschaft ein.

Datum: {{AssemblyDate}}
Uhrzeit: {{AssemblyTime}}
Ort: {{AssemblyLocation}}

Tagesordnung:
{{Agenda}}

Ihre Anwesenheit ist wichtig für die Beschlussfähigkeit der Versammlung.

Mit freundlichen Grüßen
Der Vorstand

---

Dear {{Member.FullName}},

We cordially invite you to the ordinary general assembly of our cooperative.

Date: {{AssemblyDate}}
Time: {{AssemblyTime}}
Location: {{AssemblyLocation}}

Agenda:
{{Agenda}}

Your attendance is important for the quorum of the assembly.

Best regards,
The Board",
                Type = MessageType.GeneralAssemblyNotice,
                Channel = MessageChannel.Email,
                Description = "Email template for general assembly invitations",
                Variables = @"{""AssemblyDate"": ""date"", ""AssemblyTime"": ""time"", ""AssemblyLocation"": ""string"", ""Agenda"": ""string""}",
                IsActive = true
            },

            // Share cancellation confirmation templates
            new MessageTemplate
            {
                Name = "Share Cancellation Confirmation - Email",
                Subject = "Bestätigung der Anteilskündigung - Share Cancellation Confirmation",
                Content = @"
Liebe/r {{Member.FullName}},

wir bestätigen hiermit den Erhalt Ihrer Kündigung für Ihre Genossenschaftsanteile.

Mitgliedsnummer: {{Member.MemberNumber}}
Gekündigte Anteile: {{CancelledShares}}
Kündigungsdatum: {{CancellationDate}}
Wirksam zum: {{EffectiveDate}}

Gemäß unserer Satzung beträgt die Kündigungsfrist zwei Jahre. Ihre Kündigung wird daher zum {{EffectiveDate}} wirksam.

Die Auszahlung erfolgt nach Ablauf der Kündigungsfrist und nach Genehmigung durch die Generalversammlung.

Mit freundlichen Grüßen
Ihre Genossenschaft

---

Dear {{Member.FullName}},

We hereby confirm receipt of your cancellation for your cooperative shares.

Member Number: {{Member.MemberNumber}}
Cancelled Shares: {{CancelledShares}}
Cancellation Date: {{CancellationDate}}
Effective Date: {{EffectiveDate}}

According to our bylaws, the cancellation period is two years. Your cancellation will therefore take effect on {{EffectiveDate}}.

The payout will be made after the expiration of the cancellation period and after approval by the general assembly.

Best regards,
Your Cooperative",
                Type = MessageType.ShareCancellationConfirmation,
                Channel = MessageChannel.Email,
                Description = "Email template for share cancellation confirmations",
                Variables = @"{""CancelledShares"": ""number"", ""CancellationDate"": ""date"", ""EffectiveDate"": ""date""}",
                IsActive = true
            },

            // Payment confirmation templates
            new MessageTemplate
            {
                Name = "Payment Confirmation - Email",
                Subject = "Zahlungsbestätigung - Payment Confirmation",
                Content = @"
Liebe/r {{Member.FullName}},

wir bestätigen hiermit den Eingang Ihrer Zahlung.

Mitgliedsnummer: {{Member.MemberNumber}}
Zahlungsnummer: {{PaymentNumber}}
Betrag: {{Amount}}
Zahlungsdatum: {{PaymentDate}}
Verwendungszweck: {{Purpose}}

Vielen Dank für Ihre Zahlung.

Mit freundlichen Grüßen
Ihre Genossenschaft

---

Dear {{Member.FullName}},

We hereby confirm receipt of your payment.

Member Number: {{Member.MemberNumber}}
Payment Number: {{PaymentNumber}}
Amount: {{Amount}}
Payment Date: {{PaymentDate}}
Purpose: {{Purpose}}

Thank you for your payment.

Best regards,
Your Cooperative",
                Type = MessageType.PaymentConfirmation,
                Channel = MessageChannel.Email,
                Description = "Email template for payment confirmations",
                Variables = @"{""PaymentNumber"": ""string"", ""Amount"": ""decimal"", ""PaymentDate"": ""date"", ""Purpose"": ""string""}",
                IsActive = true
            }
        };

        context.MessageTemplates.AddRange(templates);
        await context.SaveChangesAsync();
    }
}