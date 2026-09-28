using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartBatchReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CartBatchReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartBatchReceipts", x => x.Id);
                });

            // Same reasoning as AddRefreshTokens: this app always connects as a role that
            // bypasses RLS, so zero policies is correct here too.
            migrationBuilder.Sql("ALTER TABLE \"CartBatchReceipts\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartBatchReceipts");
        }
    }
}
