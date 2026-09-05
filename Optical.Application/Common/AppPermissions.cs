namespace Optical.Application.Common;

/// <summary>One module a role can be granted access to.</summary>
public sealed record PermissionDefinition(string Key, string Name, string Description);

/// <summary>
/// The modules access can be granted to. The list is fixed in code because a permission
/// only means anything if something checks it — an invented name would grant nothing.
/// Which role holds which permission is stored in the database and edited by an Admin.
/// </summary>
public static class AppPermissions
{
    public const string Dashboard = "Dashboard";
    public const string Pos = "Pos";
    public const string Sales = "Sales";
    public const string Customers = "Customers";
    public const string Catalog = "Catalog";
    public const string Inventory = "Inventory";
    public const string Purchasing = "Purchasing";
    public const string Exchanges = "Exchanges";
    public const string Reports = "Reports";
    public const string Settings = "Settings";
    public const string ManageUsers = "ManageUsers";

    /// <summary>Changing one's own display name and password.</summary>
    public const string Profile = "Profile";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Pos, "Point of Sale", "Ring up sales and take payment."),
        new(Sales, "Sales & Receipts", "View past sales and reprint receipts."),
        new(Customers, "Customers", "Add and edit customer records."),
        new(Dashboard, "Dashboard", "Today's figures, profit, margin and stock value."),
        new(Reports, "Reports", "Sales and inventory reports."),
        new(Catalog, "Products, Categories & Brands", "Create products and set sale prices."),
        new(Inventory, "Inventory", "Adjust stock levels by hand."),
        new(Purchasing, "Suppliers & Purchases", "Record purchases and increase stock."),
        new(Exchanges, "Exchanges", "Swap sold goods and collect adjustments."),
        new(Settings, "Shop Settings", "Business name and address."),
        new(ManageUsers, "Staff Accounts", "Create staff and edit roles and permissions."),
        new(Profile, "Own Profile & Password",
            "Change one's own display name and password. Without it, an Admin resets the password.")
    ];

    /// <summary>
    /// Admin must always keep this one. Without it, one careless save would leave nobody
    /// able to reach this screen and no way back in.
    /// </summary>
    public static bool IsLocked(string role, string permission) =>
        role == AppRoles.Admin && permission == ManageUsers;

    public static bool Exists(string permission) => All.Any(p => p.Key == permission);

    /// <summary>What each role starts with. Applied once, when nothing has been saved yet.</summary>
    public static IReadOnlyDictionary<string, string[]> Defaults { get; } =
        new Dictionary<string, string[]>
        {
            [AppRoles.Admin] = All.Select(p => p.Key).ToArray(),

            // The owner runs the business but does not administer the system.
            [AppRoles.Owner] = All.Select(p => p.Key).Where(k => k != ManageUsers).ToArray(),

            // The till: selling, and swapping goods against a sale it can see. Still nothing
            // that sets a price or edits stock directly — an exchange is priced from the
            // original sale line and today's product price, never by hand.
            [AppRoles.Cashier] = [Pos, Sales, Customers, Exchanges]
        };
}
