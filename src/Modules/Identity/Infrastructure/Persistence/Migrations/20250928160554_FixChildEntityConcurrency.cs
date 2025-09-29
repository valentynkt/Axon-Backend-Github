using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixChildEntityConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletOwnership_Principal_AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropIndex(
                name: "IX_WalletOwnership_AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropColumn(
                name: "AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership");

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
                name: "FK_WalletOwnership_Principal_principal_id",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.AddColumn<Guid>(
                name: "AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletOwnership_AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership",
                column: "AxonPrincipalId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOwnership_Principal_AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership",
                column: "AxonPrincipalId",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOwnership_Principal_principal_id",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
