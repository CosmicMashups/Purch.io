using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomingReceivingAndSupplierDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contacts",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specialization",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tin",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IncomingReceivingReportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    Remark = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingReceivingReportLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncomingReceivingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    AppliedToPurchaseOrder = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingReceivingReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncomingReceivingReportLines_ReportId",
                table: "IncomingReceivingReportLines",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingReceivingReports_PurchaseOrderId",
                table: "IncomingReceivingReports",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingReceivingReports_SupplierId",
                table: "IncomingReceivingReports",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncomingReceivingReportLines");

            migrationBuilder.DropTable(
                name: "IncomingReceivingReports");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Contacts",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Specialization",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Tin",
                table: "Suppliers");
        }
    }
}
