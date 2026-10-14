using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualDiscountKindValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManualDiscountKind",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountValue",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManualDiscountKind",
                table: "TransactionLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountValue",
                table: "TransactionLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualDiscountKind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ManualDiscountValue",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ManualDiscountKind",
                table: "TransactionLines");

            migrationBuilder.DropColumn(
                name: "ManualDiscountValue",
                table: "TransactionLines");
        }
    }
}
