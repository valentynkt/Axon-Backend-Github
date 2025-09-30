using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintOnAiResponseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add unique index on AiResponseId for idempotency
            // Partial index - only non-null values must be unique
            migrationBuilder.CreateIndex(
                name: "IX_Messages_AiResponseId",
                schema: "chat",
                table: "Messages",
                column: "AiResponseId",
                unique: true,
                filter: "\"AiResponseId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_AiResponseId",
                schema: "chat",
                table: "Messages");
        }
    }
}