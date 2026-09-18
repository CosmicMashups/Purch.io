using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticItemPromos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ItemPromoDiscountAmount",
                table: "Transactions",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AppliedPromoLabel",
                table: "TransactionLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoDiscountAmount",
                table: "TransactionLines",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BogoPromoRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TriggerItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerQuantity = table.Column<int>(type: "integer", nullable: false),
                    FreeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeQuantity = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BogoPromoRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComboPromoRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ItemAId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemBId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComboPrice = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboPromoRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemDiscountPromoRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemDiscountPromoRules", x => x.Id);
                });

            // These are tenant-scoped tables — see 20260913182201_EnableRowLevelSecurity's
            // doc comment for why every such table gets RLS switched on with no policies.
            migrationBuilder.Sql("ALTER TABLE \"BogoPromoRules\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE \"ComboPromoRules\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE \"ItemDiscountPromoRules\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BogoPromoRules");

            migrationBuilder.DropTable(
                name: "ComboPromoRules");

            migrationBuilder.DropTable(
                name: "ItemDiscountPromoRules");

            migrationBuilder.DropColumn(
                name: "ItemPromoDiscountAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AppliedPromoLabel",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "PromoDiscountAmount",
                table: "TransactionLines");
        }
    }
}
