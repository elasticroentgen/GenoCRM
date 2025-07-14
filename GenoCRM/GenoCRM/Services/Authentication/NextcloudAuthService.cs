using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GenoCRM.Models.Domain;
using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Services.Configuration;

namespace GenoCRM.Services.Authentication;

public interface INextcloudAuthService
{
    Task<NextcloudUser?> GetUserInfoAsync(string accessToken);
    Task<IEnumerable<string>> GetUserGroupsAsync(string accessToken, string userId);
    Task<User> SyncUserAsync(NextcloudUser nextcloudUser, IEnumerable<string> groups);
    Task UpdateUserPermissionsAsync(int userId, IEnumerable<string> groups);
    ClaimsPrincipal CreateClaimsPrincipal(User user);
}

public class NextcloudAuthService : INextcloudAuthService
{
    private readonly HttpClient _httpClient;
    private readonly GenoDbContext _context;
    private readonly ILogger<NextcloudAuthService> _logger;
    private readonly NextcloudAuthConfig _config;
    private readonly IGroupPermissionService _groupPermissionService;

    public NextcloudAuthService(
        HttpClient httpClient, 
        GenoDbContext context,
        IConfiguration configuration, 
        ILogger<NextcloudAuthService> logger,
        IGroupPermissionService groupPermissionService)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
        _config = configuration.GetSection("NextcloudAuth").Get<NextcloudAuthConfig>() ?? new NextcloudAuthConfig();
        _groupPermissionService = groupPermissionService;
    }

    public async Task<NextcloudUser?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.BaseUrl}/ocs/v1.php/cloud/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("OCS-APIRequest", "true");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get user info from Nextcloud: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = ParseNextcloudUserResponse(content);
            
            _logger.LogInformation("Retrieved user info for {UserId}", userInfo?.Id);
            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from Nextcloud");
            return null;
        }
    }

    public async Task<IEnumerable<string>> GetUserGroupsAsync(string accessToken, string userId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{_config.BaseUrl}/ocs/v1.php/cloud/users/{userId}/groups");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("OCS-APIRequest", "true");

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get user groups from Nextcloud: {StatusCode}", response.StatusCode);
                return Array.Empty<string>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var groups = ParseNextcloudGroupsResponse(content);
            
            _logger.LogInformation("Retrieved {GroupCount} groups for user {UserId}", groups.Count(), userId);
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user groups from Nextcloud for user {UserId}", userId);
            return Array.Empty<string>();
        }
    }

    public async Task<User> SyncUserAsync(NextcloudUser nextcloudUser, IEnumerable<string> groups)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.UserGroups)
                .FirstOrDefaultAsync(u => u.NextcloudUserId == nextcloudUser.Id);

            if (user == null)
            {
                user = new User
                {
                    NextcloudUserId = nextcloudUser.Id,
                    Email = nextcloudUser.Email,
                    FirstName = nextcloudUser.DisplayName?.Split(' ').FirstOrDefault() ?? nextcloudUser.Id,
                    LastName = nextcloudUser.DisplayName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    DisplayName = nextcloudUser.DisplayName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
            }
            else
            {
                // Update user information
                user.Email = nextcloudUser.Email;
                if (!string.IsNullOrEmpty(nextcloudUser.DisplayName))
                {
                    var nameParts = nextcloudUser.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    user.FirstName = nameParts.FirstOrDefault() ?? user.FirstName;
                    user.LastName = nameParts.Skip(1).FirstOrDefault() ?? user.LastName;
                    user.DisplayName = nextcloudUser.DisplayName;
                }
                user.LastLoginAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
            }

            // Update user groups
            user.UserGroups.Clear();
            foreach (var groupName in groups)
            {
                user.UserGroups.Add(new UserGroup
                {
                    GroupName = groupName,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Update permissions based on group mappings
            await UpdateUserPermissionsAsync(user.Id, groups);

            _logger.LogInformation("Synced user {UserId} with {GroupCount} groups", 
                nextcloudUser.Id, groups.Count());

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user {UserId}", nextcloudUser.Id);
            throw;
        }
    }

    public async Task UpdateUserPermissionsAsync(int userId, IEnumerable<string> groups)
    {
        try
        {
            // Get permissions from JSON configuration
            var permissions = _groupPermissionService.GetPermissionsForGroups(groups);

            // Clear existing permissions
            var existingPermissions = await _context.UserPermissions
                .Where(up => up.UserId == userId)
                .ToListAsync();
            
            _context.UserPermissions.RemoveRange(existingPermissions);

            // Add new permissions based on group memberships
            var newPermissions = permissions.Select(permission => new UserPermission
            {
                UserId = userId,
                Permission = permission,
                IsGranted = true,
                GrantedBy = "System",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.UserPermissions.AddRange(newPermissions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated {PermissionCount} permissions for user {UserId}", 
                newPermissions.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId}", userId);
            throw;
        }
    }

    public ClaimsPrincipal CreateClaimsPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("nextcloud_user_id", user.NextcloudUserId),
            new("display_name", user.DisplayName ?? user.FullName)
        };

        // Add group claims
        foreach (var group in user.UserGroups)
        {
            claims.Add(new Claim(ClaimTypes.Role, group.GroupName));
            claims.Add(new Claim("group", group.GroupName));
        }

        // Add permission claims
        foreach (var permission in user.UserPermissions.Where(p => p.IsGranted))
        {
            claims.Add(new Claim("permission", permission.Permission));
        }

        var identity = new ClaimsIdentity(claims, "NextcloudOAuth");
        return new ClaimsPrincipal(identity);
    }

    private NextcloudUser? ParseNextcloudUserResponse(string xmlContent)
    {
        try
        {
            // Parse XML response from Nextcloud OCS API
            // Simplified parsing - in production you might want to use XDocument
            var startIndex = xmlContent.IndexOf("<id>", StringComparison.Ordinal);
            var endIndex = xmlContent.IndexOf("</id>", StringComparison.Ordinal);
            
            if (startIndex == -1 || endIndex == -1) return null;
            
            var userId = xmlContent.Substring(startIndex + 4, endIndex - startIndex - 4);

            // Extract email
            var emailStart = xmlContent.IndexOf("<email>", StringComparison.Ordinal);
            var emailEnd = xmlContent.IndexOf("</email>", StringComparison.Ordinal);
            var email = emailStart != -1 && emailEnd != -1 
                ? xmlContent.Substring(emailStart + 7, emailEnd - emailStart - 7) 
                : $"{userId}@local";

            // Extract display name
            var displayStart = xmlContent.IndexOf("<displayname>", StringComparison.Ordinal);
            var displayEnd = xmlContent.IndexOf("</displayname>", StringComparison.Ordinal);
            var displayName = displayStart != -1 && displayEnd != -1 
                ? xmlContent.Substring(displayStart + 13, displayEnd - displayStart - 13) 
                : userId;

            return new NextcloudUser
            {
                Id = userId,
                Email = email,
                DisplayName = displayName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Nextcloud user response");
            return null;
        }
    }

    private IEnumerable<string> ParseNextcloudGroupsResponse(string xmlContent)
    {
        try
        {
            var groups = new List<string>();
            var startTag = "<element>";
            var endTag = "</element>";
            
            var currentIndex = 0;
            while (true)
            {
                var startIndex = xmlContent.IndexOf(startTag, currentIndex, StringComparison.Ordinal);
                if (startIndex == -1) break;
                
                var endIndex = xmlContent.IndexOf(endTag, startIndex, StringComparison.Ordinal);
                if (endIndex == -1) break;
                
                var groupName = xmlContent.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length);
                groups.Add(groupName);
                
                currentIndex = endIndex + endTag.Length;
            }
            
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Nextcloud groups response");
            return Array.Empty<string>();
        }
    }
}

public class NextcloudUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public class NextcloudAuthConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizeEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;
}