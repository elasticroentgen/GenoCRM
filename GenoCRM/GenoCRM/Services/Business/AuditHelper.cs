using GenoCRM.Models.Domain;
using System.Security.Claims;

namespace GenoCRM.Services.Business;

public static class AuditHelper
{
    public static async Task LogAuditAsync(
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        AuditAction action,
        string entityType,
        string entityId,
        string entityDescription,
        string? permission = null,
        object? changes = null)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            
            var userName = user?.Identity?.Name ?? (user?.Identity?.IsAuthenticated == true ? "Unknown" : "System");
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = httpContext?.Request?.Headers["User-Agent"].FirstOrDefault();

            await auditService.LogActionAsync(
                userName,
                action,
                entityType,
                entityId,
                entityDescription,
                permission,
                changes,
                ipAddress,
                userAgent);
        }
        catch (Exception ex)
        {
            // Log the exception but don't break business operations
            try
            {
                var loggerFactory = httpContextAccessor.HttpContext?.RequestServices?.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("AuditHelper");
                logger?.LogError(ex, "Failed to log audit entry for {Action} on {EntityType} {EntityId}", 
                    action, entityType, entityId);
            }
            catch
            {
                // If we can't even log the error, just ignore it
            }
        }
    }

    public static string GetMemberDescription(Member member)
    {
        return $"{member.FullName} ({member.MemberNumber})";
    }

    public static string GetShareDescription(CooperativeShare share)
    {
        return $"Certificate {share.CertificateNumber} ({share.Quantity} shares)";
    }

    public static string GetPaymentDescription(Payment payment)
    {
        return $"Payment {payment.PaymentNumber} (€{payment.Amount:F2})";
    }

    public static string GetDividendDescription(Dividend dividend)
    {
        return $"Dividend {dividend.FiscalYear} (€{dividend.Amount:F2})";
    }

    public static string GetShareTransferDescription(ShareTransfer transfer)
    {
        return $"Transfer {transfer.Quantity} shares";
    }

    public static string GetMessageDescription(Message message)
    {
        return $"{message.Type} via {message.Channel}";
    }

    public static string GetDocumentDescription(Document document)
    {
        return $"{document.Type}: {document.Title}";
    }

    public static string GetLoanDescription(SubordinatedLoan loan)
    {
        return $"Loan {loan.LoanNumber} (€{loan.Amount:F2})";
    }
}