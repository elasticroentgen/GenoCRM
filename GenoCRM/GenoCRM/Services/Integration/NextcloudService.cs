using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace GenoCRM.Services.Integration;

public interface INextcloudService
{
    Task<bool> UploadDocumentAsync(string fileName, Stream fileStream, string remotePath);
    Task<Stream?> DownloadDocumentAsync(string remotePath);
    Task<bool> DeleteDocumentAsync(string remotePath);
    Task<IEnumerable<NextcloudFile>> ListFilesAsync(string remotePath);
    Task<bool> CreateFolderAsync(string remotePath);
    Task<string?> CreateShareLinkAsync(string remotePath, bool isPublic = false);
    Task<bool> TestConnectionAsync();
}

public class NextcloudService : INextcloudService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NextcloudService> _logger;
    private readonly NextcloudConfig _config;

    public NextcloudService(HttpClient httpClient, IConfiguration configuration, ILogger<NextcloudService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = configuration.GetSection("Nextcloud").Get<NextcloudConfig>() ?? new NextcloudConfig();
        
        // Setup basic authentication for WebDAV
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    public async Task<bool> UploadDocumentAsync(string fileName, Stream fileStream, string remotePath)
    {
        try
        {
            var fullPath = $"{remotePath.TrimEnd('/')}/{fileName}";
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/{fullPath.TrimStart('/')}";
            
            var content = new StreamContent(fileStream);
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = content
            };
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Document uploaded successfully to {RemotePath}", fullPath);
                return true;
            }
            else
            {
                _logger.LogError("Failed to upload document to {RemotePath}: {StatusCode}", fullPath, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName} to {RemotePath}", fileName, remotePath);
            return false;
        }
    }

    public async Task<Stream?> DownloadDocumentAsync(string remotePath)
    {
        try
        {
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/{remotePath.TrimStart('/')}";
            
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Document downloaded successfully from {RemotePath}", remotePath);
                return await response.Content.ReadAsStreamAsync();
            }
            else
            {
                _logger.LogError("Failed to download document from {RemotePath}: {StatusCode}", remotePath, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document from {RemotePath}", remotePath);
            return null;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string remotePath)
    {
        try
        {
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/{remotePath.TrimStart('/')}";
            
            var response = await _httpClient.DeleteAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Document deleted successfully from {RemotePath}", remotePath);
                return true;
            }
            else
            {
                _logger.LogError("Failed to delete document from {RemotePath}: {StatusCode}", remotePath, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document from {RemotePath}", remotePath);
            return false;
        }
    }

    public async Task<IEnumerable<NextcloudFile>> ListFilesAsync(string remotePath)
    {
        try
        {
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/{remotePath.TrimStart('/')}";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url);
            request.Headers.Add("Depth", "1");
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var files = ParsePropfindResponse(content, remotePath);
                
                _logger.LogInformation("Listed {Count} files from {RemotePath}", files.Count(), remotePath);
                return files;
            }
            else
            {
                _logger.LogError("Failed to list files from {RemotePath}: {StatusCode}", remotePath, response.StatusCode);
                return new List<NextcloudFile>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from {RemotePath}", remotePath);
            return new List<NextcloudFile>();
        }
    }

    private IEnumerable<NextcloudFile> ParsePropfindResponse(string xmlContent, string basePath)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var ns = XNamespace.Get("DAV:");
            
            var responses = doc.Descendants(ns + "response");
            var files = new List<NextcloudFile>();
            
            foreach (var response in responses)
            {
                var href = response.Element(ns + "href")?.Value;
                if (string.IsNullOrEmpty(href)) continue;
                
                var propstat = response.Element(ns + "propstat");
                var prop = propstat?.Element(ns + "prop");
                
                if (prop != null)
                {
                    var isDirectory = prop.Element(ns + "resourcetype")?.Element(ns + "collection") != null;
                    var contentLength = prop.Element(ns + "getcontentlength")?.Value;
                    var lastModified = prop.Element(ns + "getlastmodified")?.Value;
                    var contentType = prop.Element(ns + "getcontenttype")?.Value;
                    
                    files.Add(new NextcloudFile
                    {
                        Name = Path.GetFileName(href.TrimEnd('/')) ?? href,
                        Path = href,
                        IsDirectory = isDirectory,
                        Size = long.TryParse(contentLength, out var size) ? size : 0,
                        LastModified = DateTime.TryParse(lastModified, out var date) ? date : null,
                        ContentType = contentType
                    });
                }
            }
            
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PROPFIND response");
            return new List<NextcloudFile>();
        }
    }

    public async Task<bool> CreateFolderAsync(string remotePath)
    {
        try
        {
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/{remotePath.TrimStart('/')}";
            
            var request = new HttpRequestMessage(new HttpMethod("MKCOL"), url);
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Folder created successfully at {RemotePath}", remotePath);
                return true;
            }
            else
            {
                _logger.LogError("Failed to create folder at {RemotePath}: {StatusCode}", remotePath, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder at {RemotePath}", remotePath);
            return false;
        }
    }

    public async Task<string?> CreateShareLinkAsync(string remotePath, bool isPublic = false)
    {
        try
        {
            var shareUrl = $"{_config.BaseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares";
            
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("path", remotePath),
                new KeyValuePair<string, string>("shareType", isPublic ? "3" : "0"),
                new KeyValuePair<string, string>("permissions", "1") // Read only
            });

            var request = new HttpRequestMessage(HttpMethod.Post, shareUrl)
            {
                Content = formData
            };

            // Add basic authentication
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            request.Headers.Add("OCS-APIRequest", "true");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Parse XML response to extract share URL
                // This is a simplified implementation - you might want to use proper XML parsing
                var urlStart = content.IndexOf("<url>") + 5;
                var urlEnd = content.IndexOf("</url>");
                if (urlStart > 4 && urlEnd > urlStart)
                {
                    var shareLink = content.Substring(urlStart, urlEnd - urlStart);
                    _logger.LogInformation("Share link created for {RemotePath}: {ShareLink}", remotePath, shareLink);
                    return shareLink;
                }
            }
            
            _logger.LogError("Failed to create share link for {RemotePath}", remotePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share link for {RemotePath}", remotePath);
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var url = $"{_config.WebDAVUrl.TrimEnd('/')}/";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url);
            request.Headers.Add("Depth", "0");
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Nextcloud connection test successful");
                return true;
            }
            else
            {
                _logger.LogError("Nextcloud connection test failed: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Nextcloud connection");
            return false;
        }
    }
}

public class NextcloudConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string WebDAVUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DocumentsPath { get; set; } = "/Documents";
}

public class NextcloudFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ContentType { get; set; }
}