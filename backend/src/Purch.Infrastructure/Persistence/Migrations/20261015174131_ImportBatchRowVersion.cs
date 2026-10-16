using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportBatchRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. The ImportBatch concurrency token maps to Postgres' built-in xmin system column,
            // which already exists on every table, so the generated AddColumn would fail. This migration records the
            // model change in the snapshot (same as AddStockTakeLineVersion).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
