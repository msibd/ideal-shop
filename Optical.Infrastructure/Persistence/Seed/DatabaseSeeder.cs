using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Optical.Application.Common;
using Optical.Domain.Entities;
using Optical.Infrastructure.Identity;

namespace Optical.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the application roles and the initial administrator account.
/// Credentials come from configuration (user-secrets / environment variables), never from source.
/// </summary>
public sealed class DatabaseSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedRolePermissionsAsync();
        await SeedAdminUserAsync();
    }

    /// <summary>
    /// Gives each role its starting modules. Only ever fills gaps for a role that has no
    /// grants at all, so an administrator's own edits are never written back over on restart.
    /// </summary>
    private async Task SeedRolePermissionsAsync()
    {
        var existing = await db.RolePermissions
            .Select(p => p.Role)
            .Distinct()
            .ToListAsync();

        var added = 0;

        foreach (var (role, permissions) in AppPermissions.Defaults)
        {
            if (existing.Contains(role))
            {
                continue;
            }

            foreach (var permission in permissions)
            {
                db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} default role permission(s).", added);
        }
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}.", role);
            }
        }
    }

    private async Task SeedAdminUserAsync()
    {
        var userName = configuration["Seed:AdminUserName"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Admin seeding skipped: Seed:AdminUserName and Seed:AdminPassword are not configured.");
            return;
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = userName,
            Email = configuration["Seed:AdminEmail"],
            EmailConfirmed = true,
            FullName = "System Administrator",
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to create the admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Seeded admin user {UserName}.", userName);
    }
}
