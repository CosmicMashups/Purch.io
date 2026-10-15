using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestockPurchaseRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Tenants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "PH");

            migrationBuilder.AddColumn<string>(
                name: "PaymentAccountName",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentAccountNumber",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlNumber",
                table: "PurchaseOrders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateNeeded",
                table: "PurchaseOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseOrders",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderTotal",
                table: "PurchaseOrders",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentAccountName",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentAccountNumber",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseRequestId",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequestDate",
                table: "PurchaseOrders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "RequestedByName",
                table: "PurchaseOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "PurchaseOrders",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "PurchaseOrders",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "VatInclusive",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "PurchaseOrders",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "PurchaseOrderLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "IncomingReceivingReportLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Branches",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DateNeeded = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByName = table.Column<string>(type: "text", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSequences_TenantId_BranchId_Kind",
                table: "DocumentSequences",
                columns: new[] { "TenantId", "BranchId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_TenantId_InventoryItemId",
                table: "ItemSuppliers",
                columns: new[] { "TenantId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSuppliers_TenantId_ItemId",
                table: "ItemSuppliers",
                columns: new[] { "TenantId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLines_PurchaseRequestId",
                table: "PurchaseRequestLines",
                column: "PurchaseRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_ControlNumber",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "ControlNumber" },
                unique: true);

            // Branch codes for existing branches: first four letters/digits of the name, numbered when a business repeats one.
            migrationBuilder.Sql("""
                UPDATE "Branches" b SET "Code" = x.code
                FROM (
                    SELECT "Id",
                        CASE WHEN rn = 1 THEN base ELSE base || rn::text END AS code
                    FROM (
                        SELECT "Id",
                            COALESCE(NULLIF(UPPER(LEFT(REGEXP_REPLACE("Name", '[^A-Za-z0-9]', '', 'g'), 4)), ''), 'BR') AS base,
                            ROW_NUMBER() OVER (PARTITION BY "TenantId", COALESCE(NULLIF(UPPER(LEFT(REGEXP_REPLACE("Name", '[^A-Za-z0-9]', '', 'g'), 4)), ''), 'BR') ORDER BY "Id") AS rn
                        FROM "Branches"
                    ) t
                ) x
                WHERE b."Id" = x."Id" AND b."Code" = '';
                """);

            // Every public table needs row level security; RowLevelSecurityCoverageTests fails the build without it.
            migrationBuilder.Sql("ALTER TABLE \"PurchaseRequests\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE \"PurchaseRequestLines\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE \"ItemSuppliers\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE \"DocumentSequences\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropTable(
                name: "ItemSuppliers");

            migrationBuilder.DropTable(
                name: "PurchaseRequestLines");

            migrationBuilder.DropTable(
                name: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PaymentAccountName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentAccountNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ControlNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DateNeeded",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "OrderTotal",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentAccountName",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentAccountNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PurchaseRequestId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RequestDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RequestedByName",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "VatInclusive",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "IncomingReceivingReportLines");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Branches");
        }
    }
}
