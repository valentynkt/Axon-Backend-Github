using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixExclusiveSigningIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the incorrect index that only has wallet_id
            migrationBuilder.DropIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership");

            // Recreate the index with all three columns
            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "wallet_id", "access_mode", "status" },
                unique: true,
                filter: "access_mode = 'Signing' AND status = 'Verified' AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the correct index
            migrationBuilder.DropIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership");

            // Recreate the incorrect index (for rollback)
            migrationBuilder.CreateIndex(
                name: "ux_exclusive_signing",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id",
                unique: true,
                filter: "status = 'Verified' AND access_mode = 'Signing' AND is_deleted = false");
        }
    }
}
