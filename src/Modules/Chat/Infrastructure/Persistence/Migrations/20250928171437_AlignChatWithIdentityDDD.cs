using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignChatWithIdentityDDD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Messages",
                schema: "chat",
                table: "Messages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Messages",
                schema: "chat",
                table: "Messages",
                columns: new[] { "ConversationId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Messages",
                schema: "chat",
                table: "Messages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Messages",
                schema: "chat",
                table: "Messages",
                column: "Id");
        }
    }
}
