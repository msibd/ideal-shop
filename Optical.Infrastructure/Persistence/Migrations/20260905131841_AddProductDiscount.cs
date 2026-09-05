using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "SaleItems",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DiscountEndsOn",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DiscountStartsOn",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_DiscountDates",
                table: "Products",
                sql: "(\"DiscountStartsOn\" IS NULL AND \"DiscountEndsOn\" IS NULL) OR (\"DiscountStartsOn\" IS NOT NULL AND \"DiscountEndsOn\" IS NOT NULL AND \"DiscountEndsOn\" >= \"DiscountStartsOn\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_DiscountPercent",
                table: "Products",
                sql: "\"DiscountPercent\" >= 0 AND 100 - \"DiscountPercent\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_DiscountDates",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_DiscountPercent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountEndsOn",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DiscountStartsOn",
                table: "Products");
        }
    }
}
