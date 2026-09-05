using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Optical.Application.Common;
using Optical.Application.Features.Permissions;

namespace Optical.Web.Security;

/// <summary>The permission a page needs, named by its module key.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Turns <c>[Authorize(Policy = AppPermissions.Reports)]</c> into a policy on demand, so
/// permissions never have to be registered one by one at startup. Anything that is not a
/// known module key falls through to the default provider.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!AppPermissions.Exists(policyName))
        {
            return fallback.GetPolicyAsync(policyName);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

/// <summary>
/// Grants access when one of the signed-in user's roles holds the permission, as recorded
/// in the database. Because the answer is read per request rather than baked into the
/// sign-in cookie, an Admin's change takes effect on the user's very next page.
/// </summary>
public sealed class PermissionAuthorizationHandler(RolePermissionStore permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roles = context.User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

        if (roles.Count == 0)
        {
            return;
        }

        if (await permissions.IsGrantedAsync(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
