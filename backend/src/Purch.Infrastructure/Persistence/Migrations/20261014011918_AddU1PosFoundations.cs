using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddU1PosFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountAmount",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ManualDiscountReason",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ParkedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParkedByUserId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkedLabel",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "VatDerived",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VatExemptSales",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRateApplied",
                table: "Transactions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatableSales",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ZeroRatedSales",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "TransactionLines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsOpenItem",
                table: "TransactionLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountAmount",
                table: "TransactionLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "TransactionLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenItemName",
                table: "TransactionLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceOverrideFrom",
                table: "TransactionLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "TransactionLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenItemEnabled",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkedCartTtlHours",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateCostOnReceiving",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "Items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InventoryItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId_Sequence",
                table: "Payments",
                columns: new[] { "TransactionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionId_Sequence",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ManualDiscountAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ManualDiscountReason",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ParkedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ParkedByUserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ParkedLabel",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VatDerived",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VatExemptSales",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VatRateApplied",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VatableSales",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ZeroRatedSales",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsOpenItem",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "ManualDiscountAmount",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "OpenItemName",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "PriceOverrideFrom",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "OpenItemEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ParkedCartTtlHours",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdateCostOnReceiving",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InventoryItems");

            // NOTE: this fails while open-item rows (TransactionLines.ItemId IS NULL) exist, because the column goes
            // back to NOT NULL. Delete or backfill those rows first before reverting this migration.
            migrationBuilder.AlterColumn<Guid>(
                name: "ItemId",
                table: "TransactionLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
