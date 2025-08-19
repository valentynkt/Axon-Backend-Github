using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeConversationTitleNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultTitle",
                schema: "chat",
                table: "conversations");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "chat",
                table: "conversations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "chat",
                table: "conversations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultTitle",
                schema: "chat",
                table: "conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
