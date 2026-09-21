using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Reporting",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_TransactionId",
                table: "TransactionLines",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedRecords_Entity",
                table: "SyncedRecords",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_BranchClosedAt",
                table: "Shifts",
                columns: new[] { "BranchId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TenantCategory",
                table: "Items",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Branch_Item_CreatedAt",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "BranchId", "ItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Actor_Action",
                table: "AuditLogs",
                columns: new[] { "TenantId", "ActorUserId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantCreatedAt",
                table: "AuditLogs",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Reporting",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_TransactionId",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_SyncedRecords_Entity",
                table: "SyncedRecords");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_BranchClosedAt",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Items_TenantCategory",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Branch_Item_CreatedAt",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Actor_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantCreatedAt",
                table: "AuditLogs");
        }
    }
}
