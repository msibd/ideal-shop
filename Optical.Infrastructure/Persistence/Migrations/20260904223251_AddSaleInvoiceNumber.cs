using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable first: the sales already on file predate invoice numbering, and a
            // NOT NULL default would give every one of them the same value to conflict on.
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Sales",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // Backfilled in the shape new sales use, numbered per day in the order they
            // were rung up. They carry the neutral INV prefix because the business-name
            // prefix did not exist when they were taken.
            migrationBuilder.Sql(
                """
                UPDATE "Sales" AS s
                SET "InvoiceNumber" = numbered."Number"
                FROM (
                    SELECT
                        "Id",
                        'INV-'
                            || to_char("CreatedAt" AT TIME ZONE 'UTC', 'YYYYMMDD')
                            || '-'
                            || lpad(
                                (row_number() OVER (
                                    PARTITION BY ("CreatedAt" AT TIME ZONE 'UTC')::date
                                    ORDER BY "Id"))::text,
                                5, '0') AS "Number"
                    FROM "Sales"
                ) AS numbered
                WHERE s."Id" = numbered."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "Sales",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Sales");
        }
    }
}
