namespace GenoCRM.Services.Storage;

public interface INextcloudDocumentService
{
    Task<bool> UploadFileAsync(string localFilePath, string nextcloudPath);
    Task<bool> UploadFileAsync(Stream fileStream, string nextcloudPath, string fileName);
    Task<Stream?> DownloadFileAsync(string nextcloudPath);
    Task<bool> DeleteFileAsync(string nextcloudPath);
    Task<bool> FileExistsAsync(string nextcloudPath);
    Task<bool> CreateDirectoryAsync(string directoryPath);
    Task<bool> DirectoryExistsAsync(string directoryPath);
    Task<IEnumerable<NextcloudFileInfo>> ListFilesAsync(string directoryPath);
    Task<NextcloudFileInfo?> GetFileInfoAsync(string nextcloudPath);
    Task<string> GetPublicShareLinkAsync(string nextcloudPath, DateTime? expirationDate = null);
    Task<bool> RevokePublicShareLinkAsync(string shareToken);
    string GetMemberDocumentPath(int memberId, string fileName);
    string GetShareDocumentPath(int shareId, string fileName);
    string GetGeneratedDocumentPath(string fileName);
}

public class NextcloudFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string ETag { get; set; } = string.Empty;
}