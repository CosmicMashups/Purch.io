using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Closes Supabase's "RLS disabled in public" advisory. This app never uses
    /// Supabase Auth, PostgREST, or the anon/authenticated keys — the Flutter
    /// client only ever talks to this .NET API, and the API's own connection
    /// string authenticates as the postgres superuser, which always bypasses
    /// RLS regardless of policy. So there is nothing here for a policy to grant:
    /// every table just needs RLS switched on with zero policies, which is an
    /// implicit deny-all for anon/authenticated (the only roles PostgREST would
    /// ever use) while leaving this backend's own queries completely unaffected.
    /// Tenant/branch/department isolation is enforced in the application layer
    /// instead (see PurchDbContext's per-ITenantScoped global query filter) —
    /// this migration is defense against the tables being reachable at all via
    /// Supabase's auto-generated REST/GraphQL APIs, not a second enforcement
    /// layer for isolation the app already owns.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        private static readonly string[] Tables =
        [
            "Tenants",
            "Branches",
            "Devices",
            "Users",
            "Categories",
            "Items",
            "ItemVariants",
            "ItemComboComponents",
            "ItemBatches",
            "BundlePromoRules",
            "ModifierGroups",
            "ItemModifiers",
            "ItemModifierGroups",
            "Departments",
            "CustomerCreditLedgers",
            "CreditTransactions",
            "Suppliers",
            "PurchaseOrders",
            "PurchaseOrderLines",
            "BranchTransfers",
            "BranchTransferLines",
            "InventoryMovements",
            "Transactions",
            "TransactionLines",
            "TransactionLineComboSelections",
            "TransactionLineModifierSelections",
            "Payments",
            "PaymentGatewayTransactions",
            "AuditLogs",
            "ReceiptSequences",
            "Shifts",
            "PromoCodes",
            "TenantMeterings",
            "SyncedRecords",
            "KioskPrepSequences",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
