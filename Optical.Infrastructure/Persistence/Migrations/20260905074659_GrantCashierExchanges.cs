using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantCashierExchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same reason as the Profile grant: the seeder leaves a role's permissions alone
            // once it has any, so a default added in code never reaches a configured database.
            // Guarded so it does nothing on a fresh install, where the seeder handles it.
            migrationBuilder.Sql("""
                INSERT INTO "RolePermissions" ("Role", "Permission", "CreatedAt")
                SELECT 'Cashier', 'Exchanges', NOW()
                WHERE EXISTS (SELECT 1 FROM "RolePermissions")
                  AND NOT EXISTS (
                      SELECT 1 FROM "RolePermissions"
                      WHERE "Role" = 'Cashier' AND "Permission" = 'Exchanges');
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "Role" = 'Cashier' AND "Permission" = 'Exchanges';
                """);

        }
    }
}
