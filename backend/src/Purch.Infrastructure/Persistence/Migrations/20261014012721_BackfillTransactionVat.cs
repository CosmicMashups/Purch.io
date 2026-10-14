using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillTransactionVat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only: derive the VAT split for sales completed before stamping existed, using the rule the
            // X/Z readings always applied (Status 2 = Completed, 4 = Refunded). Senior/PWD sales are wholly
            // exempt at TotalAmount + DiscountAmount; everything else is VAT-inclusive at 12%.
            migrationBuilder.Sql(@"
UPDATE ""Transactions"" SET
    ""VatableSales"" = CASE WHEN ""SeniorPwdDiscountApplied"" THEN 0 ELSE ROUND(""TotalAmount"" / 1.12, 2) END,
    ""VatAmount"" = CASE WHEN ""SeniorPwdDiscountApplied"" THEN 0 ELSE ""TotalAmount"" - ROUND(""TotalAmount"" / 1.12, 2) END,
    ""VatExemptSales"" = CASE WHEN ""SeniorPwdDiscountApplied"" THEN ""TotalAmount"" + ""DiscountAmount"" ELSE 0 END,
    ""ZeroRatedSales"" = 0,
    ""VatRateApplied"" = 0.12,
    ""VatDerived"" = TRUE
WHERE ""Status"" IN (2, 4) AND ""VatRateApplied"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only backfill; nothing to undo (derived rows are marked VatDerived and harmless).
        }
    }
}
