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
            migrationBuilder.DropForeignKey(
                name: "fk_wallet_ownership_principal_principal_id",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropForeignKey(
                name: "fk_wallet_ownership_principal_principal_id1",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropIndex(
                name: "ix_wallet_ownership_principal_id1",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropColumn(
                name: "principal_id1",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.AlterColumn<DateTime>(
                name: "verified_at",
                schema: "identity",
                table: "wallet_ownership",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "verification_source",
                schema: "identity",
                table: "wallet_ownership",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "revoked_at",
                schema: "identity",
                table: "wallet_ownership",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "revoke_reason",
                schema: "identity",
                table: "wallet_ownership",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "axon_principal_id",
                schema: "identity",
                table: "wallet_ownership",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "axon_user_auth",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    axon_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    dynamic_environment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dynamic_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    first_authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    primary_chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    primary_wallet_address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    concurrency_stamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_axon_user_auth", x => x.id);
                    table.ForeignKey(
                        name: "fk_axon_user_auth_axon_principal",
                        column: x => x.axon_principal_id,
                        principalSchema: "identity",
                        principalTable: "principal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownership_axon_principal_id",
                schema: "identity",
                table: "wallet_ownership",
                column: "axon_principal_id");

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_axon_principal_id",
                schema: "identity",
                table: "axon_user_auth",
                column: "axon_principal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_dynamic_user_id",
                schema: "identity",
                table: "axon_user_auth",
                column: "dynamic_user_id",
                filter: "\"DynamicUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_normalized_email",
                schema: "identity",
                table: "axon_user_auth",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_normalized_user_name",
                schema: "identity",
                table: "axon_user_auth",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_primary_wallet_address",
                schema: "identity",
                table: "axon_user_auth",
                column: "primary_wallet_address",
                filter: "\"PrimaryWalletAddress\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_axon_user_auth_provider_type_original_subject",
                schema: "identity",
                table: "axon_user_auth",
                columns: new[] { "provider_type", "original_subject" });

            migrationBuilder.AddForeignKey(
                name: "fk_wallet_ownership_principal_axon_principal_id",
                schema: "identity",
                table: "wallet_ownership",
                column: "axon_principal_id",
                principalSchema: "identity",
                principalTable: "principal",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_wallet_ownership_principal_principal_id",
                schema: "identity",
                table: "wallet_ownership",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "principal",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_wallet_ownership_principal_axon_principal_id",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropForeignKey(
                name: "fk_wallet_ownership_principal_principal_id",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropTable(
                name: "axon_user_auth",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_wallet_ownership_axon_principal_id",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.DropColumn(
                name: "axon_principal_id",
                schema: "identity",
                table: "wallet_ownership");

            migrationBuilder.AlterColumn<DateTime>(
                name: "verified_at",
                schema: "identity",
                table: "wallet_ownership",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "verification_source",
                schema: "identity",
                table: "wallet_ownership",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "revoked_at",
                schema: "identity",
                table: "wallet_ownership",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "revoke_reason",
                schema: "identity",
                table: "wallet_ownership",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "principal_id1",
                schema: "identity",
                table: "wallet_ownership",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_wallet_ownership_principal_id1",
                schema: "identity",
                table: "wallet_ownership",
                column: "principal_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_wallet_ownership_principal_principal_id",
                schema: "identity",
                table: "wallet_ownership",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "principal",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_wallet_ownership_principal_principal_id1",
                schema: "identity",
                table: "wallet_ownership",
                column: "principal_id1",
                principalSchema: "identity",
                principalTable: "principal",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
