using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "Principal",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RiskTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wallet",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallet", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Credential",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credential", x => new { x.principal_id, x.id });
                    table.ForeignKey(
                        name: "FK_Credential_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalChainDefault",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalChainDefault", x => new { x.principal_id, x.id });
                    table.ForeignKey(
                        name: "FK_PrincipalChainDefault_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletOwnership",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletOwnership", x => new { x.principal_id, x.id });
                    table.ForeignKey(
                        name: "FK_WalletOwnership_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_credential_provider",
                schema: "identity",
                table: "Credential",
                columns: new[] { "provider", "issuer", "subject" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_principal_created_at",
                schema: "identity",
                table: "Principal",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_principal_risk_tier",
                schema: "identity",
                table: "Principal",
                column: "RiskTier");

            migrationBuilder.CreateIndex(
                name: "ix_principal_type",
                schema: "identity",
                table: "Principal",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_principals_updated_at_id",
                schema: "identity",
                table: "Principal",
                columns: new[] { "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "idx_default_principal_id",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "idx_default_wallet_id",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ux_chain_default",
                schema: "identity",
                table: "PrincipalChainDefault",
                columns: new[] { "principal_id", "chain_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_wallet_chain_id",
                schema: "identity",
                table: "Wallet",
                column: "chain_id");

            migrationBuilder.CreateIndex(
                name: "idx_wallet_last_seen_at",
                schema: "identity",
                table: "Wallet",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_chain_updated",
                schema: "identity",
                table: "Wallet",
                columns: new[] { "chain_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_wallet_chain_addr",
                schema: "identity",
                table: "Wallet",
                columns: new[] { "chain_id", "address" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_wallet_id",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "wallet_id", "access_mode", "status" },
                unique: true,
                filter: "access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_ownership_pair",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "principal_id", "wallet_id" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Credential",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PrincipalChainDefault",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Wallet",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "WalletOwnership",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Principal",
                schema: "identity");
        }
    }
}
