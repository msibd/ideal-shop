using Microsoft.AspNetCore.Identity;

namespace Optical.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Disabled staff keep their history but can no longer sign in.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
