using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchManualGcashQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualGcashAccountName",
                table: "Branches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualGcashAccountNumber",
                table: "Branches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualGcashQrImageUrl",
                table: "Branches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualGcashAccountName",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ManualGcashAccountNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ManualGcashQrImageUrl",
                table: "Branches");
        }
    }
}
