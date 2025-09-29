using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingWalletOwnershipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Credential_Principal_principal_id",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropForeignKey(
                name: "FK_PrincipalChainDefault_Principal_principal_id",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOwnership_Wallet_wallet_id",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletOwnership",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "idx_ownership_principal_active",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "idx_ownership_status",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PrincipalChainDefault",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Credential",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropIndex(
                name: "idx_credential_principal_id",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropIndex(
                name: "idx_credential_provider",
                schema: "identity",
                table: "Credential");

            migrationBuilder.AddColumn<Guid>(
                name: "principal_id1",
                schema: "identity",
                table: "WalletOwnership",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "principal_id1",
                schema: "identity",
                table: "Credential",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletOwnership",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "principal_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PrincipalChainDefault",
                schema: "identity",
                table: "PrincipalChainDefault",
                columns: new[] { "principal_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Credential",
                schema: "identity",
                table: "Credential",
                columns: new[] { "principal_id", "id" });

            migrationBuilder.CreateIndex(
                name: "idx_ownership_wallet_id",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOwnership_principal_id1",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id1");

            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "wallet_id", "access_mode", "status" },
                unique: true,
                filter: "access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalChainDefault_principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "principal_id1");

            migrationBuilder.CreateIndex(
                name: "IX_Credential_principal_id1",
                schema: "identity",
                table: "Credential",
                column: "principal_id1");

            migrationBuilder.AddForeignKey(
                name: "FK_Credential_Principal_principal_id1",
                schema: "identity",
                table: "Credential",
                column: "principal_id1",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrincipalChainDefault_Principal_principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "principal_id1",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id1",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id1",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Credential_Principal_principal_id1",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropForeignKey(
                name: "FK_PrincipalChainDefault_Principal_principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id1",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WalletOwnership",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "idx_ownership_wallet_id",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "IX_WalletOwnership_principal_id1",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PrincipalChainDefault",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropIndex(
                name: "IX_PrincipalChainDefault_principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Credential",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropIndex(
                name: "IX_Credential_principal_id1",
                schema: "identity",
                table: "Credential");

            migrationBuilder.DropColumn(
                name: "principal_id1",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropColumn(
                name: "principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.DropColumn(
                name: "principal_id1",
                schema: "identity",
                table: "Credential");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WalletOwnership",
                schema: "identity",
                table: "WalletOwnership",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PrincipalChainDefault",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Credential",
                schema: "identity",
                table: "Credential",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_principal_active",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_status",
                schema: "identity",
                table: "WalletOwnership",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "wallet_id", "access_mode", "status" },
                unique: true,
                filter: "access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_credential_principal_id",
                schema: "identity",
                table: "Credential",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "idx_credential_provider",
                schema: "identity",
                table: "Credential",
                column: "provider");

            migrationBuilder.AddForeignKey(
                name: "FK_Credential_Principal_principal_id",
                schema: "identity",
                table: "Credential",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PrincipalChainDefault_Principal_principal_id",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOwnership_Wallet_wallet_id",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id",
                principalSchema: "identity",
                principalTable: "Wallet",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
