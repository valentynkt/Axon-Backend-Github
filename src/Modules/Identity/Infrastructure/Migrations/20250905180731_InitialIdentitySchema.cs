using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "axon_principals",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    primary_email_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    risk_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_axon_principals", x => x.id);
                    table.CheckConstraint("ck_axon_principals_email_hash_format", "primary_email_hash IS NULL OR (LENGTH(primary_email_hash) = 64 AND primary_email_hash ~ '^[a-f0-9]+$')");
                    table.CheckConstraint("ck_axon_principals_preferred_language", "preferred_language IN ('en', 'es', 'fr', 'de', 'ja', 'ko', 'zh')");
                    table.CheckConstraint("ck_axon_principals_risk_tier", "risk_tier IN ('low', 'medium', 'high', 'critical')");
                    table.CheckConstraint("ck_axon_principals_type", "type IN ('human', 'service')");
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_credentials",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    axon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    environment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    verification_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    session_public_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    device_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_credentials", x => x.id);
                    table.ForeignKey(
                        name: "fk_identity_credentials_axon_principals_axon_id",
                        column: x => x.axon_id,
                        principalSchema: "identity",
                        principalTable: "axon_principals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "principal_chain_defaults",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    axon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axon_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_principal_chain_defaults", x => x.id);
                    table.ForeignKey(
                        name: "fk_principal_chain_defaults_axon_principals_axon_principal_id",
                        column: x => x.axon_principal_id,
                        principalSchema: "identity",
                        principalTable: "axon_principals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallet_ownerships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    axon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    proof_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_ownerships", x => x.id);
                    table.ForeignKey(
                        name: "fk_wallet_ownerships_axon_principals_axon_id",
                        column: x => x.axon_id,
                        principalSchema: "identity",
                        principalTable: "axon_principals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallet_tags",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_tags", x => x.id);
                    table.CheckConstraint("ck_wallet_tags_allowed_values", "tag IN ('personal', 'business', 'trading', 'defi', 'gaming', 'nft', 'dao', 'test', 'main', 'hot', 'cold')");
                    table.ForeignKey(
                        name: "fk_wallet_tags_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalSchema: "identity",
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_axon_principals_created_at",
                schema: "identity",
                table: "axon_principals",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_axon_principals_is_deleted_type",
                schema: "identity",
                table: "axon_principals",
                columns: new[] { "is_deleted", "type" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_axon_principals_primary_email_hash",
                schema: "identity",
                table: "axon_principals",
                column: "primary_email_hash",
                filter: "primary_email_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_axon_principals_type",
                schema: "identity",
                table: "axon_principals",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_axon_id",
                schema: "identity",
                table: "identity_credentials",
                column: "axon_id");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_created_at",
                schema: "identity",
                table: "identity_credentials",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_last_seen_at",
                schema: "identity",
                table: "identity_credentials",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_provider_issuer_subject_unique",
                schema: "identity",
                table: "identity_credentials",
                columns: new[] { "provider_type", "issuer", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_principal_chain_defaults_axon_chain_unique",
                schema: "identity",
                table: "principal_chain_defaults",
                columns: new[] { "axon_id", "chain_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_principal_chain_defaults_axon_principal_id",
                schema: "identity",
                table: "principal_chain_defaults",
                column: "axon_principal_id");

            migrationBuilder.CreateIndex(
                name: "ix_principal_chain_defaults_chain_id",
                schema: "identity",
                table: "principal_chain_defaults",
                column: "chain_id");

            migrationBuilder.CreateIndex(
                name: "ix_principal_chain_defaults_wallet_id",
                schema: "identity",
                table: "principal_chain_defaults",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_axon_id_state",
                schema: "identity",
                table: "wallet_ownerships",
                columns: new[] { "axon_id", "state" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_chain_id",
                schema: "identity",
                table: "wallet_ownerships",
                column: "chain_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_created_at",
                schema: "identity",
                table: "wallet_ownerships",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_state_access_mode",
                schema: "identity",
                table: "wallet_ownerships",
                columns: new[] { "state", "access_mode" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_wallet_chain_deleted",
                schema: "identity",
                table: "wallet_ownerships",
                columns: new[] { "wallet_id", "chain_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownerships_wallet_id",
                schema: "identity",
                table: "wallet_ownerships",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_tags_tag",
                schema: "identity",
                table: "wallet_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_tags_wallet_tag_unique",
                schema: "identity",
                table: "wallet_tags",
                columns: new[] { "wallet_id", "tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallets_address",
                schema: "identity",
                table: "wallets",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_chain",
                schema: "identity",
                table: "wallets",
                column: "chain");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_chain_address_unique",
                schema: "identity",
                table: "wallets",
                columns: new[] { "chain", "address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallets_first_seen_at",
                schema: "identity",
                table: "wallets",
                column: "first_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_last_seen_at",
                schema: "identity",
                table: "wallets",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_provider",
                schema: "identity",
                table: "wallets",
                column: "provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "principal_chain_defaults",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "wallet_ownerships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "wallet_tags",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "axon_principals",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "wallets",
                schema: "identity");
        }
    }
}
