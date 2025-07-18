using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace GenoCRM.Services.Storage;

public class NextcloudDocumentService : INextcloudDocumentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NextcloudDocumentService> _logger;
    private readonly string _baseUrl;
    private readonly string _webDavUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _documentsPath;
    private readonly string _memberDocumentsPath;
    private readonly string _shareDocumentsPath;
    private readonly string _generatedDocumentsPath;

    public NextcloudDocumentService(HttpClient httpClient, IConfiguration configuration, ILogger<NextcloudDocumentService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _baseUrl = _configuration["Nextcloud:BaseUrl"] ?? throw new InvalidOperationException("Nextcloud BaseUrl not configured");
        _webDavUrl = _configuration["Nextcloud:WebDAVUrl"] ?? throw new InvalidOperationException("Nextcloud WebDAVUrl not configured");
        _username = _configuration["Nextcloud:Username"] ?? throw new InvalidOperationException("Nextcloud Username not configured");
        _password = _configuration["Nextcloud:Password"] ?? throw new InvalidOperationException("Nextcloud Password not configured");
        _documentsPath = _configuration["Nextcloud:DocumentsPath"] ?? "/Documents/GenoCRM";
        _memberDocumentsPath = _configuration["Nextcloud:MemberDocumentsPath"] ?? "/Documents/GenoCRM/Members";
        _shareDocumentsPath = _configuration["Nextcloud:ShareDocumentsPath"] ?? "/Documents/GenoCRM/Shares";
        _generatedDocumentsPath = _configuration["Nextcloud:GeneratedDocumentsPath"] ?? "/Documents/GenoCRM/Generated";
        
        // Setup basic authentication
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<bool> UploadFileAsync(string localFilePath, string nextcloudPath)
    {
        try
        {
            if (!File.Exists(localFilePath))
            {
                _logger.LogError("Local file not found: {LocalFilePath}", localFilePath);
                return false;
            }

            using var fileStream = File.OpenRead(localFilePath);
            return await UploadFileAsync(fileStream, nextcloudPath, Path.GetFileName(localFilePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {LocalFilePath} to {NextcloudPath}", localFilePath, nextcloudPath);
            return false;
        }
    }

    public async Task<bool> UploadFileAsync(Stream fileStream, string nextcloudPath, string fileName)
    {
        try
        {
            // Ensure directory exists
            var directoryPath = Path.GetDirectoryName(nextcloudPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(directoryPath))
            {
                await CreateDirectoryAsync(directoryPath);
            }

            var url = $"{_webDavUrl.TrimEnd('/')}/{nextcloudPath.TrimStart('/')}";
            
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
            var response = await _httpClient.PutAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully uploaded file {FileName} to {NextcloudPath}", fileName, nextcloudPath);
                return true;
            }
            
            _logger.LogError("Failed to upload file {FileName} to {NextcloudPath}. Status: {StatusCode}", 
                fileName, nextcloudPath, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to {NextcloudPath}", fileName, nextcloudPath);
            return false;
        }
    }

    public async Task<Stream?> DownloadFileAsync(string nextcloudPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{nextcloudPath.TrimStart('/')}";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStreamAsync();
            }
            
            _logger.LogError("Failed to download file {NextcloudPath}. Status: {StatusCode}", 
                nextcloudPath, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {NextcloudPath}", nextcloudPath);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(string nextcloudPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{nextcloudPath.TrimStart('/')}";
            var response = await _httpClient.DeleteAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted file {NextcloudPath}", nextcloudPath);
                return true;
            }
            
            _logger.LogError("Failed to delete file {NextcloudPath}. Status: {StatusCode}", 
                nextcloudPath, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {NextcloudPath}", nextcloudPath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string nextcloudPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{nextcloudPath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists {NextcloudPath}", nextcloudPath);
            return false;
        }
    }

    public async Task<bool> CreateDirectoryAsync(string directoryPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{directoryPath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Add("Content-Type", "application/x-directory");
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                // MethodNotAllowed means directory already exists
                return true;
            }
            
            _logger.LogError("Failed to create directory {DirectoryPath}. Status: {StatusCode}", 
                directoryPath, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<bool> DirectoryExistsAsync(string directoryPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{directoryPath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if directory exists {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<IEnumerable<NextcloudFileInfo>> ListFilesAsync(string directoryPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{directoryPath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Depth", "1");
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to list files in directory {DirectoryPath}. Status: {StatusCode}", 
                    directoryPath, response.StatusCode);
                return new List<NextcloudFileInfo>();
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return ParseWebDavResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in directory {DirectoryPath}", directoryPath);
            return new List<NextcloudFileInfo>();
        }
    }

    public async Task<NextcloudFileInfo?> GetFileInfoAsync(string nextcloudPath)
    {
        try
        {
            var url = $"{_webDavUrl.TrimEnd('/')}/{nextcloudPath.TrimStart('/')}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Depth", "0");
            
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var files = ParseWebDavResponse(content);
            return files.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info for {NextcloudPath}", nextcloudPath);
            return null;
        }
    }

    public async Task<string> GetPublicShareLinkAsync(string nextcloudPath, DateTime? expirationDate = null)
    {
        try
        {
            var url = $"{_baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares";
            var data = new
            {
                path = nextcloudPath,
                shareType = 3, // Public link
                expireDate = expirationDate?.ToString("yyyy-MM-dd")
            };
            
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("OCS-APIRequest", "true");
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                // Parse OCS response to get share URL
                // This is a simplified implementation - you may need to parse XML response
                return $"{_baseUrl}/s/[SHARE_TOKEN]";
            }
            
            _logger.LogError("Failed to create public share link for {NextcloudPath}. Status: {StatusCode}", 
                nextcloudPath, response.StatusCode);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating public share link for {NextcloudPath}", nextcloudPath);
            return string.Empty;
        }
    }

    public async Task<bool> RevokePublicShareLinkAsync(string shareToken)
    {
        try
        {
            var url = $"{_baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares/{shareToken}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("OCS-APIRequest", "true");
            
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking public share link {ShareToken}", shareToken);
            return false;
        }
    }

    public string GetMemberDocumentPath(int memberId, string fileName)
    {
        return $"{_memberDocumentsPath.TrimEnd('/')}/{memberId:D6}/{fileName}";
    }

    public string GetShareDocumentPath(int shareId, string fileName)
    {
        return $"{_shareDocumentsPath.TrimEnd('/')}/{shareId:D6}/{fileName}";
    }

    public string GetGeneratedDocumentPath(string fileName)
    {
        return $"{_generatedDocumentsPath.TrimEnd('/')}/{DateTime.UtcNow:yyyy-MM}/{fileName}";
    }

    private IEnumerable<NextcloudFileInfo> ParseWebDavResponse(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var ns = XNamespace.Get("DAV:");
            
            var files = new List<NextcloudFileInfo>();
            
            foreach (var response in doc.Descendants(ns + "response"))
            {
                var href = response.Element(ns + "href")?.Value;
                if (string.IsNullOrEmpty(href)) continue;
                
                var propstat = response.Element(ns + "propstat");
                var prop = propstat?.Element(ns + "prop");
                
                if (prop == null) continue;
                
                var displayName = prop.Element(ns + "displayname")?.Value ?? Path.GetFileName(href);
                var contentLength = prop.Element(ns + "getcontentlength")?.Value;
                var lastModified = prop.Element(ns + "getlastmodified")?.Value;
                var contentType = prop.Element(ns + "getcontenttype")?.Value ?? "application/octet-stream";
                var resourceType = prop.Element(ns + "resourcetype");
                var etag = prop.Element(ns + "getetag")?.Value ?? string.Empty;
                
                var isDirectory = resourceType?.Element(ns + "collection") != null;
                
                files.Add(new NextcloudFileInfo
                {
                    Name = displayName,
                    Path = href,
                    Size = long.TryParse(contentLength, out var size) ? size : 0,
                    LastModified = DateTime.TryParse(lastModified, out var modified) ? modified : DateTime.MinValue,
                    ContentType = contentType,
                    IsDirectory = isDirectory,
                    ETag = etag
                });
            }
            
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing WebDAV response");
            return new List<NextcloudFileInfo>();
        }
    }
}