using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Storage;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace GenoCRM.Services.Business;

public class DocumentService : IDocumentService
{
    private readonly GenoDbContext _context;
    private readonly INextcloudDocumentService _nextcloudService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DocumentService(GenoDbContext context, INextcloudDocumentService nextcloudService, 
        IConfiguration configuration, ILogger<DocumentService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _nextcloudService = nextcloudService;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Document> UploadDocumentAsync(IBrowserFile file, int? memberId = null, int? shareId = null, 
        DocumentType documentType = DocumentType.Other, string? description = null)
    {
        try
        {
            // Validate file
            if (!await IsFileTypeAllowedAsync(file.Name))
            {
                throw new InvalidOperationException($"File type not allowed: {Path.GetExtension(file.Name)}");
            }

            if (!await IsFileSizeAllowedAsync(file.Size))
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size");
            }

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.Name);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            
            // Determine Nextcloud path
            string nextcloudPath;
            if (memberId.HasValue)
            {
                nextcloudPath = _nextcloudService.GetMemberDocumentPath(memberId.Value, uniqueFileName);
            }
            else if (shareId.HasValue)
            {
                nextcloudPath = _nextcloudService.GetShareDocumentPath(shareId.Value, uniqueFileName);
            }
            else
            {
                nextcloudPath = _nextcloudService.GetGeneratedDocumentPath(uniqueFileName);
            }

            // Upload to Nextcloud
            using var stream = file.OpenReadStream();
            var uploadSuccess = await _nextcloudService.UploadFileAsync(stream, nextcloudPath, uniqueFileName);
            
            if (!uploadSuccess)
            {
                throw new InvalidOperationException("Failed to upload file to Nextcloud");
            }

            // Calculate file hash
            stream.Position = 0;
            var fileHash = await CalculateFileHashAsync(stream);

            // Create document record
            var document = new Document
            {
                MemberId = memberId,
                ShareId = shareId,
                Title = Path.GetFileNameWithoutExtension(file.Name),
                FileName = uniqueFileName,
                ContentType = file.ContentType,
                FileSize = file.Size,
                Type = documentType,
                Description = description,
                NextcloudPath = nextcloudPath,
                CreatedBy = GetCurrentUserName(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Create initial version
            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                FileName = uniqueFileName,
                FileSize = file.Size,
                NextcloudPath = nextcloudPath,
                CreatedBy = GetCurrentUserName(),
                CreatedAt = DateTime.UtcNow
            };

            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document uploaded successfully: {DocumentId} - {FileName}", document.Id, file.Name);
            
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document: {FileName}", file.Name);
            throw;
        }
    }

    public async Task<Document> CreateGeneratedDocumentAsync(string fileName, byte[] content, string contentType, 
        int? memberId = null, int? shareId = null, DocumentType documentType = DocumentType.GeneratedDocument, 
        string? description = null)
    {
        try
        {
            // Generate unique filename
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            
            // Determine Nextcloud path
            string nextcloudPath;
            if (memberId.HasValue)
            {
                nextcloudPath = _nextcloudService.GetMemberDocumentPath(memberId.Value, uniqueFileName);
            }
            else if (shareId.HasValue)
            {
                nextcloudPath = _nextcloudService.GetShareDocumentPath(shareId.Value, uniqueFileName);
            }
            else
            {
                nextcloudPath = _nextcloudService.GetGeneratedDocumentPath(uniqueFileName);
            }

            // Upload to Nextcloud
            using var stream = new MemoryStream(content);
            var uploadSuccess = await _nextcloudService.UploadFileAsync(stream, nextcloudPath, uniqueFileName);
            
            if (!uploadSuccess)
            {
                throw new InvalidOperationException("Failed to upload generated document to Nextcloud");
            }

            // Calculate file hash
            stream.Position = 0;
            var fileHash = await CalculateFileHashAsync(stream);

            // Create document record
            var document = new Document
            {
                MemberId = memberId,
                ShareId = shareId,
                Title = Path.GetFileNameWithoutExtension(fileName),
                FileName = uniqueFileName,
                ContentType = contentType,
                FileSize = content.Length,
                Type = documentType,
                Description = description,
                NextcloudPath = nextcloudPath,
                CreatedBy = GetCurrentUserName(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Create initial version
            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                FileName = uniqueFileName,
                FileSize = content.Length,
                NextcloudPath = nextcloudPath,
                CreatedBy = GetCurrentUserName(),
                CreatedAt = DateTime.UtcNow
            };

            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated document created successfully: {DocumentId} - {FileName}", document.Id, fileName);
            
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating generated document: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Document?> GetDocumentByIdAsync(int id)
    {
        try
        {
            return await _context.Documents
                .Include(d => d.Member)
                .Include(d => d.Share)
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document by ID: {DocumentId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetDocumentsByMemberIdAsync(int memberId)
    {
        try
        {
            return await _context.Documents
                .Include(d => d.Member)
                .Include(d => d.Share)
                .Where(d => d.MemberId == memberId && d.Status == DocumentStatus.Active)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents by member ID: {MemberId}", memberId);
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetDocumentsByShareIdAsync(int shareId)
    {
        try
        {
            return await _context.Documents
                .Include(d => d.Member)
                .Include(d => d.Share)
                .Where(d => d.ShareId == shareId && d.Status == DocumentStatus.Active)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents by share ID: {ShareId}", shareId);
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
    {
        try
        {
            return await _context.Documents
                .Include(d => d.Member)
                .Include(d => d.Share)
                .Where(d => d.Status == DocumentStatus.Active)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all documents");
            throw;
        }
    }

    public async Task<Stream?> DownloadDocumentAsync(int documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null || string.IsNullOrEmpty(document.NextcloudPath))
            {
                return null;
            }

            return await _nextcloudService.DownloadFileAsync(document.NextcloudPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return false;
            }

            // Mark as deleted instead of physical deletion
            document.Status = DocumentStatus.Deleted;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedBy = GetCurrentUserName();

            await _context.SaveChangesAsync();

            // Optionally delete from Nextcloud
            if (!string.IsNullOrEmpty(document.NextcloudPath))
            {
                await _nextcloudService.DeleteFileAsync(document.NextcloudPath);
            }

            _logger.LogInformation("Document deleted: {DocumentId}", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> UpdateDocumentAsync(Document document)
    {
        try
        {
            var existingDocument = await _context.Documents.FindAsync(document.Id);
            if (existingDocument == null)
            {
                return false;
            }

            existingDocument.Title = document.Title;
            existingDocument.Description = document.Description;
            existingDocument.Tags = document.Tags;
            existingDocument.Type = document.Type;
            existingDocument.IsConfidential = document.IsConfidential;
            existingDocument.ExpirationDate = document.ExpirationDate;
            existingDocument.UpdatedAt = DateTime.UtcNow;
            existingDocument.UpdatedBy = GetCurrentUserName();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document updated: {DocumentId}", document.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document: {DocumentId}", document.Id);
            throw;
        }
    }

    public async Task<string> GetPublicShareLinkAsync(int documentId, DateTime? expirationDate = null)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null || string.IsNullOrEmpty(document.NextcloudPath))
            {
                return string.Empty;
            }

            var shareLink = await _nextcloudService.GetPublicShareLinkAsync(document.NextcloudPath, expirationDate);
            
            if (!string.IsNullOrEmpty(shareLink))
            {
                document.NextcloudShareLink = shareLink;
                document.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return shareLink;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating public share link for document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> RevokePublicShareLinkAsync(int documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null || string.IsNullOrEmpty(document.NextcloudShareLink))
            {
                return false;
            }

            // Extract share token from link - this is a simplified approach
            var shareToken = document.NextcloudShareLink.Split('/').LastOrDefault();
            if (string.IsNullOrEmpty(shareToken))
            {
                return false;
            }

            var revoked = await _nextcloudService.RevokePublicShareLinkAsync(shareToken);
            
            if (revoked)
            {
                document.NextcloudShareLink = null;
                document.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return revoked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking public share link for document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> IsFileTypeAllowedAsync(string fileName)
    {
        try
        {
            var allowedExtensions = _configuration.GetSection("Nextcloud:AllowedExtensions").Get<string[]>();
            if (allowedExtensions == null || allowedExtensions.Length == 0)
            {
                return true; // Allow all if not configured
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file type: {FileName}", fileName);
            return false;
        }
    }

    public Task<bool> IsFileSizeAllowedAsync(long fileSize)
    {
        try
        {
            var maxFileSize = _configuration.GetValue<long>("Nextcloud:MaxFileSize");
            if (maxFileSize <= 0)
            {
                return Task.FromResult(true); // Allow all if not configured
            }

            return Task.FromResult(fileSize <= maxFileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file size: {FileSize}", fileSize);
            return Task.FromResult(false);
        }
    }

    public async Task<DocumentVersion> CreateDocumentVersionAsync(int documentId, IBrowserFile file, string? changeDescription = null)
    {
        try
        {
            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                throw new InvalidOperationException("Document not found");
            }

            // Validate file
            if (!await IsFileTypeAllowedAsync(file.Name))
            {
                throw new InvalidOperationException($"File type not allowed: {Path.GetExtension(file.Name)}");
            }

            if (!await IsFileSizeAllowedAsync(file.Size))
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size");
            }

            // Generate unique filename for new version
            var fileExtension = Path.GetExtension(file.Name);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            
            // Use same path structure as original document
            var nextcloudPath = Path.GetDirectoryName(document.NextcloudPath)?.Replace('\\', '/') + "/" + uniqueFileName;

            // Upload to Nextcloud
            using var stream = file.OpenReadStream();
            var uploadSuccess = await _nextcloudService.UploadFileAsync(stream, nextcloudPath, uniqueFileName);
            
            if (!uploadSuccess)
            {
                throw new InvalidOperationException("Failed to upload file version to Nextcloud");
            }

            // Get next version number
            var nextVersionNumber = document.Versions.Max(v => v.VersionNumber) + 1;

            // Create version record
            var version = new DocumentVersion
            {
                DocumentId = documentId,
                VersionNumber = nextVersionNumber,
                FileName = uniqueFileName,
                FileSize = file.Size,
                NextcloudPath = nextcloudPath,
                ChangeDescription = changeDescription,
                CreatedBy = GetCurrentUserName(),
                CreatedAt = DateTime.UtcNow
            };

            _context.DocumentVersions.Add(version);

            // Update document metadata
            document.FileName = uniqueFileName;
            document.FileSize = file.Size;
            document.NextcloudPath = nextcloudPath;
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedBy = GetCurrentUserName();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document version created: {DocumentId} - Version {VersionNumber}", documentId, nextVersionNumber);
            
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document version: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentVersion>> GetDocumentVersionsAsync(int documentId)
    {
        try
        {
            return await _context.DocumentVersions
                .Where(v => v.DocumentId == documentId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document versions: {DocumentId}", documentId);
            throw;
        }
    }

    private async Task<string> CalculateFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private string GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
    }
}