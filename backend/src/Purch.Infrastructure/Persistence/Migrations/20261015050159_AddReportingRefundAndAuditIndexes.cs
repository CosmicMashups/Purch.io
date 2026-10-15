using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingRefundAndAuditIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Refunded",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId", "RefundedAt" },
                filter: "\"RefundedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "TenantId", "ActionType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Refunded",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs");
        }
    }
}
