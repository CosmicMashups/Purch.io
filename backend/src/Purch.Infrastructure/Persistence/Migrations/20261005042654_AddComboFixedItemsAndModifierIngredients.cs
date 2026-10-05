using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComboFixedItemsAndModifierIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChoiceUpchargesJson",
                table: "ItemComboComponents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComponentItemId",
                table: "ItemComboComponents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemModifierIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemModifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityPerOrder = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemModifierIngredients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemModifierIngredients_ItemModifierId",
                table: "ItemModifierIngredients",
                column: "ItemModifierId");

            // Like every other table: switched on with no policies, so Supabase's auto-generated APIs cannot read it.
            migrationBuilder.Sql("ALTER TABLE \"ItemModifierIngredients\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemModifierIngredients");

            migrationBuilder.DropColumn(
                name: "ChoiceUpchargesJson",
                table: "ItemComboComponents");

            migrationBuilder.DropColumn(
                name: "ComponentItemId",
                table: "ItemComboComponents");
        }
    }
}
