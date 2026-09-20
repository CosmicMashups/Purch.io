using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOneOpenPerDeviceAndMoreConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The concurrency tokens added to Shifts, ReceiptSequences, PurchaseOrders, PurchaseOrderLines and
            // BranchTransfers map to Postgres' built-in xmin system column, which already exists on every
            // table - the generated AddColumn would fail - so they need no schema change here; the model
            // snapshot records them.

            // A device may have only one open cart and one open shift. Nothing enforced that until now, so
            // existing data can hold duplicates left by racing requests, and the unique indexes below would
            // refuse to build. Keep the newest of each and retire the older ones: a duplicate open cart is
            // voided (status 3), a duplicate open shift is closed (status 1) with a note saying why.
            migrationBuilder.Sql("""
                UPDATE "Transactions" AS t
                SET "Status" = 3
                WHERE t."Status" = 0
                  AND EXISTS (
                      SELECT 1 FROM "Transactions" AS newer
                      WHERE newer."DeviceId" = t."DeviceId"
                        AND newer."Status" = 0
                        AND (newer."CreatedAt" > t."CreatedAt"
                             OR (newer."CreatedAt" = t."CreatedAt" AND newer."Id" > t."Id")));
                """);

            migrationBuilder.Sql("""
                UPDATE "Shifts" AS s
                SET "Status" = 1,
                    "ClosedAt" = now(),
                    "HandoverNotes" = COALESCE(s."HandoverNotes" || ' ', '')
                        || '[Closed automatically by a database migration: a newer shift was open on this device.]'
                WHERE s."Status" = 0
                  AND EXISTS (
                      SELECT 1 FROM "Shifts" AS newer
                      WHERE newer."DeviceId" = s."DeviceId"
                        AND newer."Status" = 0
                        AND (newer."OpenedAt" > s."OpenedAt"
                             OR (newer."OpenedAt" = s."OpenedAt" AND newer."Id" > s."Id")));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OneOpenCartPerDevice",
                table: "Transactions",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_OneOpenShiftPerDevice",
                table: "Shifts",
                column: "DeviceId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_OneOpenCartPerDevice",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_OneOpenShiftPerDevice",
                table: "Shifts");
        }
    }
}
