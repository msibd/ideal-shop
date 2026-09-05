using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Sales taken before discounts existed were never reduced, so what was charged is
            // also what the lines came to. Filling SubTotal in keeps every historical receipt
            // adding up, and keeps the new check constraint true for rows already in the table.
            migrationBuilder.Sql("UPDATE \"Sales\" SET \"SubTotal\" = \"TotalAmount\";");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_DiscountAmount",
                table: "Sales",
                sql: "\"DiscountAmount\" >= 0 AND \"SubTotal\" - \"DiscountAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItems_DiscountAmount",
                table: "SaleItems",
                sql: "\"DiscountAmount\" >= 0 AND \"Quantity\" * \"UnitPrice\" - \"DiscountAmount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_DiscountAmount",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItems_DiscountAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleItems");
        }
    }
}
