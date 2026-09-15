using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingColorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old single "theme color" was the brand/primary action colour,
            // so it carries over to the accent slot rather than being dropped.
            // (EF's scaffolder guessed SecondaryText purely by column ordering.)
            migrationBuilder.RenameColumn(
                name: "BrandingThemeColorHex",
                table: "Tenants",
                newName: "BrandingAccentColorHex");

            migrationBuilder.AddColumn<string>(
                name: "BrandingSecondaryTextColorHex",
                table: "Tenants",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingBackgroundColorHex",
                table: "Tenants",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandingPrimaryTextColorHex",
                table: "Tenants",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandingSecondaryTextColorHex",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BrandingBackgroundColorHex",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BrandingPrimaryTextColorHex",
                table: "Tenants");

            migrationBuilder.RenameColumn(
                name: "BrandingAccentColorHex",
                table: "Tenants",
                newName: "BrandingThemeColorHex");
        }
    }
}
