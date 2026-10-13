using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipPinLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinLookup",
                table: "Memberships",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_PinLookup",
                table: "Memberships",
                columns: new[] { "TenantId", "PinLookup" },
                filter: "\"PinLookup\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_TenantId_PinLookup",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "PinLookup",
                table: "Memberships");
        }
    }
}
