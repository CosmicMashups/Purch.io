using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds Transactions.ClientSaleId — the client-generated idempotency key for the
    /// one-call checkout — with a filtered unique index per tenant, so a retried
    /// checkout (or two concurrent ones carrying the same id) can never create two sales.
    /// </summary>
    [DbContext(typeof(PurchDbContext))]
    [Migration("20260921000000_AddTransactionClientSaleId")]
    public partial class AddTransactionClientSaleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientSaleId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_ClientSaleId",
                table: "Transactions",
                columns: new[] { "TenantId", "ClientSaleId" },
                unique: true,
                filter: "\"ClientSaleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_ClientSaleId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ClientSaleId",
                table: "Transactions");
        }
    }
}
