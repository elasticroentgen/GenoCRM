using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IShareTransferService
{
    Task<ShareTransfer> CreateShareTransferRequestAsync(int fromMemberId, int toMemberId, int shareId, int quantity);
    Task<bool> ApproveShareTransferAsync(int transferId, string approvedBy);
    Task<bool> RejectShareTransferAsync(int transferId, string rejectedBy, string reason);
    Task<bool> CompleteShareTransferAsync(int transferId);
    Task<bool> CancelShareTransferAsync(int transferId);
    Task<ShareTransfer?> GetShareTransferByIdAsync(int id);
    Task<IEnumerable<ShareTransfer>> GetShareTransfersByMemberAsync(int memberId);
    Task<IEnumerable<ShareTransfer>> GetAllShareTransfersAsync();
    Task<IEnumerable<ShareTransfer>> GetPendingShareTransfersAsync();
    Task<bool> CanTransferSharesAsync(int fromMemberId, int toMemberId, int shareId, int quantity);
    Task<bool> ValidateShareTransferAsync(int fromMemberId, int toMemberId, int shareId, int quantity);
}