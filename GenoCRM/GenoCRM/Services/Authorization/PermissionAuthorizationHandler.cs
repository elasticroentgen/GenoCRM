using Microsoft.AspNetCore.Authorization;

namespace GenoCRM.Services.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Check if user has the required permission
        var hasPermission = context.User.HasClaim("permission", requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class GroupRequirement : IAuthorizationRequirement
{
    public string GroupName { get; }

    public GroupRequirement(string groupName)
    {
        GroupName = groupName;
    }
}

public class GroupAuthorizationHandler : AuthorizationHandler<GroupRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupRequirement requirement)
    {
        // Check if user is in the required group
        var isInGroup = context.User.HasClaim("group", requirement.GroupName) ||
                       context.User.IsInRole(requirement.GroupName);

        if (isInGroup)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Authorization policy provider
public static class AuthorizationPolicies
{
    // Permission-based policies
    public const string ViewMembers = "ViewMembers";
    public const string CreateMembers = "CreateMembers";
    public const string EditMembers = "EditMembers";
    public const string DeleteMembers = "DeleteMembers";
    
    public const string ViewShares = "ViewShares";
    public const string CreateShares = "CreateShares";
    public const string EditShares = "EditShares";
    public const string CancelShares = "CancelShares";
    
    public const string ViewDividends = "ViewDividends";
    public const string CalculateDividends = "CalculateDividends";
    public const string ApproveDividends = "ApproveDividends";
    public const string PayDividends = "PayDividends";
    
    public const string ViewDocuments = "ViewDocuments";
    public const string ManageDocuments = "ManageDocuments";
    
    public const string ViewLoans = "ViewLoans";
    public const string ManageLoans = "ManageLoans";
    
    public const string Administration = "Administration";
    public const string ViewReports = "ViewReports";
    
    // Group-based policies
    public const string AdminGroup = "AdminGroup";
    public const string ManagerGroup = "ManagerGroup";
    public const string MemberGroup = "MemberGroup";
    public const string ViewerGroup = "ViewerGroup";

    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        // Permission-based policies using the correct permission constants
        options.AddPolicy(ViewMembers, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewMembers)));
        
        options.AddPolicy(CreateMembers, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.CreateMembers)));
        
        options.AddPolicy(EditMembers, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.EditMembers)));
        
        options.AddPolicy(DeleteMembers, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.DeleteMembers)));

        options.AddPolicy(ViewShares, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewShares)));
        
        options.AddPolicy(CreateShares, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.CreateShares)));
        
        options.AddPolicy(EditShares, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.EditShares)));
        
        options.AddPolicy(CancelShares, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.CancelShares)));

        options.AddPolicy(ViewDividends, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewDividends)));
        
        options.AddPolicy(CalculateDividends, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.CalculateDividends)));
        
        options.AddPolicy(ApproveDividends, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ApproveDividends)));
        
        options.AddPolicy(PayDividends, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.PayDividends)));

        options.AddPolicy(ViewDocuments, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewDocuments)));
        
        options.AddPolicy(ManageDocuments, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ManageDocuments)));

        options.AddPolicy(ViewLoans, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewLoans)));
        
        options.AddPolicy(ManageLoans, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.CreateLoans)));

        options.AddPolicy(Administration, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ManageUsers)));
        
        options.AddPolicy(ViewReports, policy => 
            policy.Requirements.Add(new PermissionRequirement(Models.Domain.Permissions.ViewReports)));

        // Group-based policies
        options.AddPolicy(AdminGroup, policy => 
            policy.Requirements.Add(new GroupRequirement("Entwickler")));
        
        options.AddPolicy(ManagerGroup, policy => 
            policy.Requirements.Add(new GroupRequirement("Vorstand")));
        
        options.AddPolicy(MemberGroup, policy => 
            policy.Requirements.Add(new GroupRequirement("member")));
        
        options.AddPolicy(ViewerGroup, policy => 
            policy.Requirements.Add(new GroupRequirement("viewer")));
    }
}