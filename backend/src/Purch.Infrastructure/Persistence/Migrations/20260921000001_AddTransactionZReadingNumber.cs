using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds Transactions.ZReadingNumber so a BIR reading covers "every completed sale not yet reported"
    /// instead of "receipt numbers above the last reading" — a sale that syncs late (its number already
    /// below the last reading's) would otherwise never appear in any reading.
    ///
    /// Existing sales already covered by a past Z-reading are marked 0 ("reported before this was
    /// tracked"), using each device's LastZReadingReceiptNumber, so the first reading after this
    /// migration doesn't re-report the whole history.
    /// </summary>
    [DbContext(typeof(PurchDbContext))]
    [Migration("20260921000001_AddTransactionZReadingNumber")]
    public partial class AddTransactionZReadingNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZReadingNumber",
                table: "Transactions",
                type: "integer",
                nullable: true);

            // Status 2 = TransactionStatus.Completed.
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" AS t
                SET "ZReadingNumber" = 0
                FROM "ReceiptSequences" AS s
                WHERE t."TenantId" = s."TenantId"
                  AND t."BranchId" = s."BranchId"
                  AND t."DeviceId" = s."DeviceId"
                  AND t."Status" = 2
                  AND t."ReceiptNumber" IS NOT NULL
                  AND t."ReceiptNumber" <= s."LastZReadingReceiptNumber";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZReadingNumber",
                table: "Transactions");
        }
    }
}
