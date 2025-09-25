using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNavigationEntityConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "WalletOwnership");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "PrincipalChainDefault");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
