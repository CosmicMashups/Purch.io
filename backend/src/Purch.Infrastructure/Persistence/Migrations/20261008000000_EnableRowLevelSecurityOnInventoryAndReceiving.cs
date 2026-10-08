using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Security fix: these four tables were created by later migrations that forgot the
    /// "ENABLE ROW LEVEL SECURITY" step every other table has (see EnableRowLevelSecurity),
    /// leaving them readable through Supabase's auto-generated REST API with the public anon key.
    /// No model change, so the snapshot is untouched.
    /// </summary>
    [DbContext(typeof(PurchDbContext))]
    [Migration("20261008000000_EnableRowLevelSecurityOnInventoryAndReceiving")]
    public partial class EnableRowLevelSecurityOnInventoryAndReceiving : Migration
    {
        private static readonly string[] Tables =
        [
            "InventoryItems",
            "ItemRecipeLines",
            "IncomingReceivingReports",
            "IncomingReceivingReportLines",
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY;");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately a no-op: rolling back must never re-expose tenant data.
        }
    }
}
