using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Exchanges");

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "Exchanges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Exchanges",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_Reason",
                table: "Exchanges",
                column: "Reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exchanges_Reason",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Exchanges");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Exchanges");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Exchanges",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
