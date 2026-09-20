using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. The concurrency tokens on Transactions, Items, InventoryItems and
            // CustomerCreditLedgers map to Postgres' built-in xmin system column, which already
            // exists on every table — the generated AddColumn would fail. This migration exists to
            // record the model change in the snapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
