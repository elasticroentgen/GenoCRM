using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IShareApprovalService
{
    Task<ShareApproval> CreateShareApprovalRequestAsync(int memberId, int requestedQuantity);
    Task<bool> ApproveShareRequestAsync(int approvalId, string approvedBy);
    Task<bool> RejectShareRequestAsync(int approvalId, string rejectedBy, string reason);
    Task<bool> CompleteShareApprovalAsync(int approvalId);
    Task<ShareApproval?> GetShareApprovalByIdAsync(int id);
    Task<IEnumerable<ShareApproval>> GetShareApprovalsByMemberAsync(int memberId);
    Task<IEnumerable<ShareApproval>> GetPendingShareApprovalsAsync();
    Task<bool> CanRequestAdditionalSharesAsync(int memberId, int requestedQuantity, int? excludeApprovalId = null);
    Task<bool> HasMemberCompletedInitialShareAsync(int memberId);
}