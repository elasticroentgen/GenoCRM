using GenoCRM.Models.Domain;

namespace GenoCRM.Services.PDF;

public interface IPdfGenerationService
{
    Task<byte[]> GenerateShareCertificateAsync(CooperativeShare share);
    Task<byte[]> GenerateSharePurchaseAgreementAsync(CooperativeShare share);
    Task<byte[]> GeneratePaymentReceiptAsync(Payment payment);
    Task<byte[]> GenerateMembershipCertificateAsync(Member member);
    Task<byte[]> GenerateShareTransferDocumentAsync(ShareTransfer transfer);
    Task<byte[]> GenerateCustomDocumentAsync(string templateName, Dictionary<string, object> data);
}

public class PdfTemplateData
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? LogoPath { get; set; }
    public string? FooterText { get; set; }
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
}