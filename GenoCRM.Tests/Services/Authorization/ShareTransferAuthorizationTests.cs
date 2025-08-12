using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using GenoCRM.Services.Authorization;
using GenoCRM.Models.Domain;
using Xunit;

namespace GenoCRM.Tests.Services.Authorization;

public class ShareTransferAuthorizationTests : IDisposable
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ServiceProvider _serviceProvider;

    public ShareTransferAuthorizationTests()
    {
        var services = new ServiceCollection();
        
        // Add authorization services
        services.AddAuthorization(AuthorizationPolicies.ConfigurePolicies);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, GroupAuthorizationHandler>();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public async Task BoardMember_ShouldHaveShareTransferApprovalPermission()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "John Doe"),
            new(ClaimTypes.Role, "Vorstand"),
            new("permission", Permissions.ApproveShareTransfers)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.ApproveShareTransfers);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task BoardMember_ShouldHaveShareTransferRejectPermission()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "John Doe"),
            new(ClaimTypes.Role, "Vorstand"),
            new("permission", Permissions.RejectShareTransfers)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.RejectShareTransfers);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task RegularMember_ShouldNotHaveShareTransferApprovalPermission()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Jane Smith"),
            new(ClaimTypes.Role, "member"),
            new("permission", Permissions.ViewShares)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.ApproveShareTransfers);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RegularMember_ShouldNotHaveShareTransferRejectPermission()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Jane Smith"),
            new(ClaimTypes.Role, "member"),
            new("permission", Permissions.ViewShares)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.RejectShareTransfers);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SupervisoryBoardMember_ShouldHaveShareTransferApprovalPermission()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Hans Mueller"),
            new(ClaimTypes.Role, "Aufsichtsrat"),
            new("permission", Permissions.ApproveShareTransfers)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.ApproveShareTransfers);

        // Assert
        Assert.True(result.Succeeded);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}