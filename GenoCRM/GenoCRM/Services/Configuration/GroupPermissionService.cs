using System.Text.Json;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Configuration;

public interface IGroupPermissionService
{
    IEnumerable<string> GetPermissionsForGroup(string groupName);
    IEnumerable<string> GetPermissionsForGroups(IEnumerable<string> groupNames);
    IDictionary<string, List<string>> GetAllGroupPermissions();
    bool HasPermission(IEnumerable<string> userGroups, string permission);
}

public class GroupPermissionService : IGroupPermissionService
{
    private readonly IDictionary<string, List<string>> _groupPermissions;
    private readonly ILogger<GroupPermissionService> _logger;

    public GroupPermissionService(IConfiguration configuration, ILogger<GroupPermissionService> logger)
    {
        _logger = logger;
        _groupPermissions = LoadGroupPermissions(configuration);
    }

    public IEnumerable<string> GetPermissionsForGroup(string groupName)
    {
        if (_groupPermissions.TryGetValue(groupName.ToLowerInvariant(), out var permissions))
        {
            return permissions;
        }

        _logger.LogWarning("No permissions found for group: {GroupName}", groupName);
        return Enumerable.Empty<string>();
    }

    public IEnumerable<string> GetPermissionsForGroups(IEnumerable<string> groupNames)
    {
        var allPermissions = new HashSet<string>();
        
        foreach (var groupName in groupNames)
        {
            var permissions = GetPermissionsForGroup(groupName);
            foreach (var permission in permissions)
            {
                allPermissions.Add(permission);
            }
        }

        return allPermissions;
    }

    public IDictionary<string, List<string>> GetAllGroupPermissions()
    {
        return _groupPermissions;
    }

    public bool HasPermission(IEnumerable<string> userGroups, string permission)
    {
        var userPermissions = GetPermissionsForGroups(userGroups);
        return userPermissions.Contains(permission);
    }

    private IDictionary<string, List<string>> LoadGroupPermissions(IConfiguration configuration)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "group-permissions.json");
            
            if (!File.Exists(configPath))
            {
                _logger.LogError("Group permissions file not found at: {ConfigPath}", configPath);
                return new Dictionary<string, List<string>>();
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<GroupPermissionConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config?.GroupPermissions == null)
            {
                _logger.LogError("Invalid group permissions configuration");
                return new Dictionary<string, List<string>>();
            }

            // Convert to case-insensitive dictionary
            var result = new Dictionary<string, List<string>>();
            foreach (var kvp in config.GroupPermissions)
            {
                result[kvp.Key.ToLowerInvariant()] = kvp.Value;
            }

            _logger.LogInformation("Loaded permissions for {GroupCount} groups", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading group permissions configuration");
            return new Dictionary<string, List<string>>();
        }
    }
}

public class GroupPermissionConfig
{
    public Dictionary<string, List<string>> GroupPermissions { get; set; } = new();
}