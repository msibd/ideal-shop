namespace Optical.Application.Common;

/// <summary>
/// Role names used for authorization. Kept as constants so views, controllers
/// and the seeder never rely on magic strings.
///
/// The roles are deliberately fixed rather than editable: in a single shop, three
/// well-understood roles beat a permission matrix nobody can reason about.
/// </summary>
public static class AppRoles
{
    /// <summary>Runs the system. The only role that can manage staff accounts.</summary>
    public const string Admin = "Admin";

    /// <summary>Runs the business: sells, buys, prices, and sees the money.</summary>
    public const string Owner = "Owner";

    /// <summary>Works the till. Cannot change anything that decides price or stock.</summary>
    public const string Cashier = "Cashier";

    /// <summary>Everything behind the counter: pricing, stock, purchasing and the figures.</summary>
    public const string AdminOrOwner = $"{Admin},{Owner}";

    /// <summary>The till and the customer book — anyone who serves a customer.</summary>
    public const string AnyStaff = $"{Admin},{Owner},{Cashier}";

    public static readonly string[] All = [Admin, Owner, Cashier];

    /// <summary>Roles an administrator may hand out. Same as All today; kept separate
    /// so a future role that is never assignable by hand does not leak into the form.</summary>
    public static readonly string[] Assignable = [Admin, Owner, Cashier];
}
