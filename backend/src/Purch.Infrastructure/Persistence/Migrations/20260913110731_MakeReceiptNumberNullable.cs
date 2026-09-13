using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeReceiptNumberNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_BranchId_DeviceId_ReceiptNumber",
                table: "Transactions");

            migrationBuilder.AlterColumn<long>(
                name: "ReceiptNumber",
                table: "Transactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_BranchId_DeviceId_ReceiptNumber",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId", "DeviceId", "ReceiptNumber" },
                unique: true,
                filter: "\"ReceiptNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_BranchId_DeviceId_ReceiptNumber",
                table: "Transactions");

            migrationBuilder.AlterColumn<long>(
                name: "ReceiptNumber",
                table: "Transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_BranchId_DeviceId_ReceiptNumber",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId", "DeviceId", "ReceiptNumber" },
                unique: true);
        }
    }
}
