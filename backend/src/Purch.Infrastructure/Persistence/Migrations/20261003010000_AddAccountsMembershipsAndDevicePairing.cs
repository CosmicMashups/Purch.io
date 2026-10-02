using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsMembershipsAndDevicePairing : Migration
    {
        private static readonly string[] NewTables = ["Accounts", "Memberships", "MembershipBranches", "EnrolmentInvites", "DeviceCredentials"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_PairingCode",
                table: "Devices");

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedRegisterDeviceId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PairedAt",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PairingCodeExpiresAt",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PairingCodeHash",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupabaseUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialHash = table.Column<string>(type: "text", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrolmentInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Duties = table.Column<int>(type: "integer", nullable: false),
                    BranchIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrolmentInvites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Duties = table.Column<int>(type: "integer", nullable: false),
                    PinHash = table.Column<string>(type: "text", nullable: true),
                    PinFailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    PinLockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memberships_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipBranches_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PairingCode",
                table: "Devices",
                column: "PairingCode",
                unique: true,
                filter: "\"PairingCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PairingCodeHash",
                table: "Devices",
                column: "PairingCodeHash",
                unique: true,
                filter: "\"PairingCodeHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SupabaseUserId",
                table: "Accounts",
                column: "SupabaseUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredentials_CredentialHash",
                table: "DeviceCredentials",
                column: "CredentialHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCredentials_DeviceId",
                table: "DeviceCredentials",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrolmentInvites_TenantId_Email",
                table: "EnrolmentInvites",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrolmentInvites_TokenHash",
                table: "EnrolmentInvites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBranches_MembershipId_BranchId",
                table: "MembershipBranches",
                columns: new[] { "MembershipId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_AccountId",
                table: "Memberships",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_LegacyUserId",
                table: "Memberships",
                column: "LegacyUserId",
                filter: "\"LegacyUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_AccountId",
                table: "Memberships",
                columns: new[] { "TenantId", "AccountId" },
                unique: true);

            // Same as every other table (see EnableRowLevelSecurity): switched on with no policies, so Supabase's
            // auto-generated REST and GraphQL APIs cannot read any of this. The API's own connection bypasses it.
            foreach (var table in NewTables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCredentials");

            migrationBuilder.DropTable(
                name: "EnrolmentInvites");

            migrationBuilder.DropTable(
                name: "MembershipBranches");

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Devices_PairingCode",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_PairingCodeHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LinkedRegisterDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PairedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PairingCodeExpiresAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PairingCodeHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Devices");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PairingCode",
                table: "Devices",
                column: "PairingCode",
                unique: true);
        }
    }
}
