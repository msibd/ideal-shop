using Optical.Domain.Common;

namespace Optical.Domain.Entities;

/// <summary>
/// Grants one role access to one module. A row's presence is the grant; revoking
/// deletes the row. The permission key comes from the fixed list in the Application
/// layer, so an unknown key can never be stored.
/// </summary>
public class RolePermission : BaseEntity
{
    public string Role { get; set; } = string.Empty;

    public string Permission { get; set; } = string.Empty;
}
