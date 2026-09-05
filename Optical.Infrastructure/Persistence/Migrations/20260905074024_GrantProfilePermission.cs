using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantProfilePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding a module to the code list does not reach a database that already has
            // permissions configured: the seeder deliberately leaves a role's grants alone
            // once it has any, so it never writes over an administrator's own edits.
            // On a fresh install this does nothing and the seeder supplies the defaults.
            migrationBuilder.Sql("""
                INSERT INTO "RolePermissions" ("Role", "Permission", "CreatedAt")
                SELECT role_name, 'Profile', NOW()
                FROM (VALUES ('Admin'), ('Owner')) AS seed(role_name)
                WHERE EXISTS (SELECT 1 FROM "RolePermissions")
                  AND NOT EXISTS (
                      SELECT 1 FROM "RolePermissions" existing
                      WHERE existing."Role" = seed.role_name
                        AND existing."Permission" = 'Profile');
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "Permission" = 'Profile';
                """);

        }
    }
}
