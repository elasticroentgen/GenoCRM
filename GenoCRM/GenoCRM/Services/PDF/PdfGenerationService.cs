using GenoCRM.Models.Domain;
using GenoCRM.Services.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using DocumentModel = GenoCRM.Models.Domain.Document;

namespace GenoCRM.Services.PDF;

public class PdfGenerationService : IPdfGenerationService
{
    private readonly IFormattingService _formattingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfGenerationService> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public PdfGenerationService(IFormattingService formattingService, IConfiguration configuration, 
        ILogger<PdfGenerationService> logger, IWebHostEnvironment webHostEnvironment)
    {
        _formattingService = formattingService;
        _configuration = configuration;
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<byte[]> GenerateShareCertificateAsync(CooperativeShare share)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = "Share Certificate",
                Variables = new Dictionary<string, object>
                {
                    ["CertificateNumber"] = share.CertificateNumber,
                    ["MemberName"] = share.Member?.FullName ?? "Unknown Member",
                    ["MemberNumber"] = share.Member?.MemberNumber ?? "Unknown",
                    ["ShareQuantity"] = share.Quantity,
                    ["NominalValue"] = _formattingService.FormatCurrency(share.NominalValue),
                    ["TotalValue"] = _formattingService.FormatCurrency(share.TotalValue),
                    ["IssueDate"] = _formattingService.FormatDate(share.IssueDate),
                    ["Status"] = share.Status.ToString(),
                    ["CooperativeName"] = "GenoCRM Cooperative"
                }
            };

            var html = await GenerateHtmlFromTemplateAsync("ShareCertificate", templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating share certificate for share {ShareId}", share.Id);
            throw;
        }
    }

    public async Task<byte[]> GenerateSharePurchaseAgreementAsync(CooperativeShare share)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = "Share Purchase Agreement",
                Variables = new Dictionary<string, object>
                {
                    ["CertificateNumber"] = share.CertificateNumber,
                    ["MemberName"] = share.Member?.FullName ?? "Unknown Member",
                    ["MemberNumber"] = share.Member?.MemberNumber ?? "Unknown",
                    ["MemberAddress"] = GetMemberAddress(share.Member),
                    ["ShareQuantity"] = share.Quantity,
                    ["NominalValue"] = _formattingService.FormatCurrency(share.NominalValue),
                    ["TotalValue"] = _formattingService.FormatCurrency(share.TotalValue),
                    ["IssueDate"] = _formattingService.FormatDate(share.IssueDate),
                    ["AgreementDate"] = _formattingService.FormatDate(DateTime.UtcNow),
                    ["CooperativeName"] = "GenoCRM Cooperative"
                }
            };

            var html = await GenerateHtmlFromTemplateAsync("SharePurchaseAgreement", templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating share purchase agreement for share {ShareId}", share.Id);
            throw;
        }
    }

    public async Task<byte[]> GeneratePaymentReceiptAsync(Payment payment)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = "Payment Receipt",
                Variables = new Dictionary<string, object>
                {
                    ["ReceiptNumber"] = $"RCP-{payment.Id:D6}",
                    ["MemberName"] = payment.Share?.Member?.FullName ?? "Unknown Member",
                    ["MemberNumber"] = payment.Share?.Member?.MemberNumber ?? "Unknown",
                    ["PaymentDate"] = _formattingService.FormatDate(payment.PaymentDate),
                    ["Amount"] = _formattingService.FormatCurrency(payment.Amount),
                    ["PaymentMethod"] = payment.Method.ToString(),
                    ["CertificateNumber"] = payment.Share?.CertificateNumber ?? "N/A",
                    ["Description"] = payment.Notes ?? "Share payment",
                    ["CooperativeName"] = "GenoCRM Cooperative"
                }
            };

            var html = await GenerateHtmlFromTemplateAsync("PaymentReceipt", templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payment receipt for payment {PaymentId}", payment.Id);
            throw;
        }
    }

    public async Task<byte[]> GenerateMembershipCertificateAsync(Member member)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = "Membership Certificate",
                Variables = new Dictionary<string, object>
                {
                    ["MemberName"] = member.FullName,
                    ["MemberNumber"] = member.MemberNumber,
                    ["MemberAddress"] = GetMemberAddress(member),
                    ["JoinDate"] = _formattingService.FormatDate(member.JoinDate),
                    ["MembershipType"] = member.MemberType.ToString(),
                    ["IssueDate"] = _formattingService.FormatDate(DateTime.UtcNow),
                    ["CooperativeName"] = "GenoCRM Cooperative"
                }
            };

            var html = await GenerateHtmlFromTemplateAsync("MembershipCertificate", templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating membership certificate for member {MemberId}", member.Id);
            throw;
        }
    }

    public async Task<byte[]> GenerateShareTransferDocumentAsync(ShareTransfer transfer)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = "Share Transfer Document",
                Variables = new Dictionary<string, object>
                {
                    ["TransferNumber"] = $"TRF-{transfer.Id:D6}",
                    ["FromMemberName"] = transfer.FromMember?.FullName ?? "Unknown Member",
                    ["FromMemberNumber"] = transfer.FromMember?.MemberNumber ?? "Unknown",
                    ["ToMemberName"] = transfer.ToMember?.FullName ?? "Unknown Member",
                    ["ToMemberNumber"] = transfer.ToMember?.MemberNumber ?? "Unknown",
                    ["CertificateNumber"] = transfer.Share?.CertificateNumber ?? "N/A",
                    ["ShareQuantity"] = transfer.Quantity,
                    ["TotalValue"] = _formattingService.FormatCurrency(transfer.TotalValue),
                    ["RequestDate"] = _formattingService.FormatDate(transfer.RequestDate),
                    ["CompletionDate"] = transfer.CompletionDate.HasValue ? _formattingService.FormatDate(transfer.CompletionDate.Value) : "Pending",
                    ["Status"] = transfer.Status.ToString(),
                    ["CooperativeName"] = "GenoCRM Cooperative"
                }
            };

            var html = await GenerateHtmlFromTemplateAsync("ShareTransferDocument", templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating share transfer document for transfer {TransferId}", transfer.Id);
            throw;
        }
    }

    public async Task<byte[]> GenerateCustomDocumentAsync(string templateName, Dictionary<string, object> data)
    {
        try
        {
            var templateData = new PdfTemplateData
            {
                Title = data.ContainsKey("Title") ? data["Title"].ToString() ?? "Document" : "Document",
                Variables = data
            };

            var html = await GenerateHtmlFromTemplateAsync(templateName, templateData);
            return ConvertHtmlToPdf(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating custom document with template {TemplateName}", templateName);
            throw;
        }
    }

    private async Task<string> GenerateHtmlFromTemplateAsync(string templateName, PdfTemplateData data)
    {
        try
        {
            var templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Templates", "PDF", $"{templateName}.html");
            
            if (!File.Exists(templatePath))
            {
                // Generate default template if not found
                return GenerateDefaultTemplate(data);
            }

            var template = await File.ReadAllTextAsync(templatePath);
            
            // Replace variables in template
            var html = template;
            foreach (var variable in data.Variables)
            {
                html = html.Replace($"{{{{{variable.Key}}}}}", variable.Value?.ToString() ?? "");
            }

            // Replace common template variables
            html = html.Replace("{{Title}}", data.Title);
            html = html.Replace("{{GeneratedDate}}", _formattingService.FormatDateTime(data.GeneratedDate));
            
            return html;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating HTML from template {TemplateName}", templateName);
            return GenerateDefaultTemplate(data);
        }
    }

    private string GenerateDefaultTemplate(PdfTemplateData data)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <title>" + data.Title + "</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("        .header { text-align: center; margin-bottom: 30px; }");
        html.AppendLine("        .content { margin: 20px 0; }");
        html.AppendLine("        .footer { text-align: center; margin-top: 30px; font-size: 12px; color: #666; }");
        html.AppendLine("        table { width: 100%; border-collapse: collapse; }");
        html.AppendLine("        th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"header\">");
        html.AppendLine("        <h1>" + data.Title + "</h1>");
        html.AppendLine("    </div>");
        html.AppendLine("    <div class=\"content\">");
        html.AppendLine("        <table>");
        
        foreach (var variable in data.Variables)
        {
            html.AppendLine($"            <tr><th>{variable.Key}</th><td>{variable.Value}</td></tr>");
        }
        
        html.AppendLine("        </table>");
        html.AppendLine("    </div>");
        html.AppendLine("    <div class=\"footer\">");
        html.AppendLine("        <p>Generated on " + _formattingService.FormatDateTime(data.GeneratedDate) + "</p>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private byte[] ConvertHtmlToPdf(string html)
    {
        try
        {
            // Configure QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
            
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    
                    page.Header()
                        .Text("GenoCRM Document")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);
                    
                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Text(StripHtmlTags(html));
                    
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Generated on ");
                            x.Span(_formattingService.FormatDateTime(DateTime.UtcNow)).SemiBold();
                        });
                });
            });
            
            return document.GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting HTML to PDF");
            throw;
        }
    }
    
    private string StripHtmlTags(string html)
    {
        // Simple HTML tag removal - for better results, consider using HtmlAgilityPack
        var stripped = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(stripped);
    }

    private string GetMemberAddress(Member? member)
    {
        if (member == null) return "Unknown Address";
        
        var address = new StringBuilder();
        if (!string.IsNullOrEmpty(member.Street))
            address.AppendLine(member.Street);
        
        if (!string.IsNullOrEmpty(member.PostalCode) || !string.IsNullOrEmpty(member.City))
        {
            address.AppendLine($"{member.PostalCode} {member.City}".Trim());
        }
        
        if (!string.IsNullOrEmpty(member.Country))
            address.AppendLine(member.Country);
        
        return address.ToString().Trim();
    }
}