using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierGroupItemLinkAndTingiConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackagedSize",
                table: "Items",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TingiAllowedSizesJson",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TingiIncrementStep",
                table: "Items",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TingiMode",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ItemModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModifierGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemModifierGroups_ItemId_ModifierGroupId",
                table: "ItemModifierGroups",
                columns: new[] { "ItemId", "ModifierGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemModifierGroups");

            migrationBuilder.DropColumn(
                name: "PackagedSize",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TingiAllowedSizesJson",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TingiIncrementStep",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TingiMode",
                table: "Items");
        }
    }
}
