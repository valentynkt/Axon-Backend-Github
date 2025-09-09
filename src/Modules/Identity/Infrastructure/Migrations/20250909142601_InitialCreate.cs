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
            migrationBuilder.DropIndex(
                name: "ix_principal_chain_defaults_axon_chain_unique",
                schema: "identity",
                table: "principal_chain_defaults");

            migrationBuilder.DropIndex(
                name: "ix_principal_chain_defaults_axon_principal_id",
                schema: "identity",
                table: "principal_chain_defaults");

            migrationBuilder.DropColumn(
                name: "axon_id",
                schema: "identity",
                table: "principal_chain_defaults");

            migrationBuilder.CreateIndex(
                name: "ix_principal_chain_defaults_axon_chain_unique",
                schema: "identity",
                table: "principal_chain_defaults",
                columns: new[] { "axon_principal_id", "chain_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_principal_chain_defaults_axon_chain_unique",
                schema: "identity",
                table: "principal_chain_defaults");

            migrationBuilder.AddColumn<Guid>(
                name: "axon_id",
                schema: "identity",
                table: "principal_chain_defaults",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
        }
    }
}
