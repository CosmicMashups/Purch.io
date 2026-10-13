using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollingAndOrdersIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Completed",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_KioskAwaitingPayment",
                table: "Transactions",
                columns: new[] { "TenantId", "BranchId" },
                filter: "\"Status\" = 1 AND \"OriginatedFromKiosk\"");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Staff",
                table: "Transactions",
                columns: new[] { "TenantId", "StaffUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierSelections_TransactionLineId",
                table: "TransactionLineModifierSelections",
                column: "TransactionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ComboSelections_TransactionLineId",
                table: "TransactionLineComboSelections",
                column: "TransactionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBatches_ItemId",
                table: "ItemBatches",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Tenant_LinkedItem",
                table: "InventoryItems",
                columns: new[] { "TenantId", "LinkedItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_LedgerId",
                table: "CreditTransactions",
                column: "CustomerCreditLedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_TransactionId",
                table: "CreditTransactions",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Completed",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_KioskAwaitingPayment",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Staff",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_ModifierSelections_TransactionLineId",
                table: "TransactionLineModifierSelections");

            migrationBuilder.DropIndex(
                name: "IX_ComboSelections_TransactionLineId",
                table: "TransactionLineComboSelections");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_ItemBatches_ItemId",
                table: "ItemBatches");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_Tenant_LinkedItem",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_CreditTransactions_LedgerId",
                table: "CreditTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CreditTransactions_TransactionId",
                table: "CreditTransactions");
        }
    }
}
