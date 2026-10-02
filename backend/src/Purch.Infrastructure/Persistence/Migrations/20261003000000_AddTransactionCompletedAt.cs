using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PurchDbContext))]
    [Migration("20261003000000_AddTransactionCompletedAt")]
    public partial class AddTransactionCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            // Sales completed before this column existed: the last time the row changed is the closest record of when it was paid.
            migrationBuilder.Sql("UPDATE \"Transactions\" SET \"CompletedAt\" = COALESCE(\"UpdatedAt\", \"CreatedAt\") WHERE \"ReceiptNumber\" IS NOT NULL AND \"CompletedAt\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Transactions");
        }
    }
}
