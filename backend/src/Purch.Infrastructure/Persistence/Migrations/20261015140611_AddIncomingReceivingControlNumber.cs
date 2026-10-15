using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomingReceivingControlNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControlNumber",
                table: "IncomingReceivingReports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ControlNumber",
                table: "IncomingReceivingReports");
        }
    }
}
