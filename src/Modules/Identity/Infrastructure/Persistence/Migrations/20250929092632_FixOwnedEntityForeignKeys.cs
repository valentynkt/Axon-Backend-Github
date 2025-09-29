using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixOwnedEntityForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropIndex(
                name: "IX_WalletOwnership_principal_id1",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "IX_PrincipalChainDefault_principal_id1",
                schema: "identity",
                table: "PrincipalChainDefault");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "IX_WalletOwnership_principal_id1",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id1");

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
    }
}
