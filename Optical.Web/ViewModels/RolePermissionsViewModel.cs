namespace Optical.Web.ViewModels;

public class PermissionRowViewModel
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required HashSet<string> GrantedTo { get; init; }

    public bool IsGrantedTo(string role) => GrantedTo.Contains(role);
}

public class RolePermissionsViewModel
{
    public List<PermissionRowViewModel> Rows { get; init; } = [];
}
