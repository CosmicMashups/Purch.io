using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalCredentialsAndMembershipRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MembershipId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocalCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalCredentials_Email",
                table: "LocalCredentials",
                column: "Email",
                unique: true);

            // Like every other table: switched on with no policies, so Supabase's auto-generated APIs cannot read it.
            migrationBuilder.Sql("ALTER TABLE \"LocalCredentials\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalCredentials");

            migrationBuilder.DropColumn(
                name: "MembershipId",
                table: "RefreshTokens");
        }
    }
}
