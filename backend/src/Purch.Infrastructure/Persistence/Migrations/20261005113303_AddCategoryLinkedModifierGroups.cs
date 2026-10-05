using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryLinkedModifierGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ItemModifierId",
                table: "TransactionLineModifierSelections",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                table: "TransactionLineModifierSelections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifierGroupId",
                table: "TransactionLineModifierSelections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceCharged",
                table: "TransactionLineModifierSelections",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "ModifierGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModifierGroupCategoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceOverride = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    IsExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierGroupCategoryItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroupCategoryItems_ModifierGroupId_ItemId",
                table: "ModifierGroupCategoryItems",
                columns: new[] { "ModifierGroupId", "ItemId" },
                unique: true);

            migrationBuilder.Sql("ALTER TABLE \"ModifierGroupCategoryItems\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModifierGroupCategoryItems");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "TransactionLineModifierSelections");

            migrationBuilder.DropColumn(
                name: "ModifierGroupId",
                table: "TransactionLineModifierSelections");

            migrationBuilder.DropColumn(
                name: "PriceCharged",
                table: "TransactionLineModifierSelections");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ModifierGroups");

            migrationBuilder.AlterColumn<Guid>(
                name: "ItemModifierId",
                table: "TransactionLineModifierSelections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
