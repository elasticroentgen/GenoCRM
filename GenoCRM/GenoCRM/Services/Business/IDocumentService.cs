using GenoCRM.Models.Domain;
using Microsoft.AspNetCore.Components.Forms;

namespace GenoCRM.Services.Business;

public interface IDocumentService
{
    Task<Document> UploadDocumentAsync(IBrowserFile file, int? memberId = null, int? shareId = null, 
        DocumentType documentType = DocumentType.Other, string? description = null);
    Task<Document> CreateGeneratedDocumentAsync(string fileName, byte[] content, string contentType, 
        int? memberId = null, int? shareId = null, DocumentType documentType = DocumentType.GeneratedDocument, 
        string? description = null);
    Task<Document?> GetDocumentByIdAsync(int id);
    Task<IEnumerable<Document>> GetDocumentsByMemberIdAsync(int memberId);
    Task<IEnumerable<Document>> GetDocumentsByShareIdAsync(int shareId);
    Task<IEnumerable<Document>> GetAllDocumentsAsync();
    Task<Stream?> DownloadDocumentAsync(int documentId);
    Task<bool> DeleteDocumentAsync(int documentId);
    Task<bool> UpdateDocumentAsync(Document document);
    Task<string> GetPublicShareLinkAsync(int documentId, DateTime? expirationDate = null);
    Task<bool> RevokePublicShareLinkAsync(int documentId);
    Task<bool> IsFileTypeAllowedAsync(string fileName);
    Task<bool> IsFileSizeAllowedAsync(long fileSize);
    Task<DocumentVersion> CreateDocumentVersionAsync(int documentId, IBrowserFile file, string? changeDescription = null);
    Task<IEnumerable<DocumentVersion>> GetDocumentVersionsAsync(int documentId);
}