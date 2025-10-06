using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncIdentityDbContextModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_wallets_chain_updated",
                schema: "identity",
                table: "Wallet",
                columns: new[] { "chain_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_principals_updated_at_id",
                schema: "identity",
                table: "Principal",
                columns: new[] { "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wallets_chain_updated",
                schema: "identity",
                table: "Wallet");

            migrationBuilder.DropIndex(
                name: "ix_principals_updated_at_id",
                schema: "identity",
                table: "Principal");
        }
    }
}
