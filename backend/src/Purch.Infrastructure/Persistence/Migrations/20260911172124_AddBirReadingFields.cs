using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBirReadingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrandAccumulatedSales",
                table: "ReceiptSequences",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastZReadingAt",
                table: "ReceiptSequences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastZReadingReceiptNumber",
                table: "ReceiptSequences",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ZReadingResetCounter",
                table: "ReceiptSequences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MachineIdentificationNumber",
                table: "Devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrandAccumulatedSales",
                table: "ReceiptSequences");

            migrationBuilder.DropColumn(
                name: "LastZReadingAt",
                table: "ReceiptSequences");

            migrationBuilder.DropColumn(
                name: "LastZReadingReceiptNumber",
                table: "ReceiptSequences");

            migrationBuilder.DropColumn(
                name: "ZReadingResetCounter",
                table: "ReceiptSequences");

            migrationBuilder.DropColumn(
                name: "MachineIdentificationNumber",
                table: "Devices");
        }
    }
}
