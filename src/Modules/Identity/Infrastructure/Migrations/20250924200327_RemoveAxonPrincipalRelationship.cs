using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAxonPrincipalRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AxonUserAuth_AxonPrincipal",
                schema: "identity",
                table: "AxonUserAuth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AxonUserAuth_AxonPrincipal",
                schema: "identity",
                table: "AxonUserAuth",
                column: "AxonPrincipalId",
                principalSchema: "identity",
                principalTable: "Principal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
