using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionToNavigationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_ownership_wallet_active",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "WalletOwnership",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "PrincipalChainDefault",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id",
                unique: true,
                filter: "status = 'Verified' AND access_mode = 'Signing' AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "PrincipalChainDefault");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_wallet_active",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id",
                unique: true,
                filter: "is_deleted = false");
        }
    }
}
