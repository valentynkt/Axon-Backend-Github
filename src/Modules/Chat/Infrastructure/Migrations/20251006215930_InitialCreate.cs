using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastAiResponseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AiResponseId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => new { x.ConversationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "chat",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_created_at_id",
                schema: "chat",
                table: "Conversations",
                columns: new[] { "OwnerId", "CreatedAt", "Id" },
                filter: "\"Status\" != 'Deleted'");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_status_updated",
                schema: "chat",
                table: "Conversations",
                columns: new[] { "OwnerId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_updated_at_id",
                schema: "chat",
                table: "Conversations",
                columns: new[] { "OwnerId", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_title_search",
                schema: "chat",
                table: "Conversations",
                column: "Title",
                filter: "\"Title\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AiResponseId",
                schema: "chat",
                table: "Messages",
                column: "AiResponseId",
                unique: true,
                filter: "\"AiResponseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "chat",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_Sequence",
                schema: "chat",
                table: "Messages",
                columns: new[] { "ConversationId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "chat");
        }
    }
}
