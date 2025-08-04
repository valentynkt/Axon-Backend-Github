using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Modules.Chat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gin", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_stat_statements", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    user_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    version = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.CheckConstraint("ck_conversations_audit_valid", "created_at_utc IS NOT NULL AND updated_at_utc IS NOT NULL AND updated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_conversations_completed_at_logic", "(status = 'Active' AND completed_at IS NULL) OR (status IN ('Completed', 'Archived') AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_conversations_status_valid", "status IN ('Active', 'Completed', 'Archived')");
                    table.CheckConstraint("ck_conversations_title_valid", "title IS NOT NULL AND LENGTH(TRIM(title)) > 0 AND LENGTH(title) <= 500");
                });

            migrationBuilder.CreateTable(
                name: "dead_letter_messages",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: false),
                    failure_stack_trace = table.Column<string>(type: "text", nullable: false),
                    processing_attempts = table.Column<int>(type: "integer", nullable: false),
                    original_occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    moved_to_dead_letter_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reprocessed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_reprocessed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letter_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_correlations",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<string>(type: "text", nullable: false),
                    causation_id = table.Column<string>(type: "text", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    machine_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_correlations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_retry_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "chat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    updated_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "system")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.CheckConstraint("ck_messages_audit_valid", "created_at_utc IS NOT NULL AND updated_at_utc IS NOT NULL AND updated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_messages_content_valid", "content IS NOT NULL AND LENGTH(TRIM(content)) > 0 AND LENGTH(content) <= 100000");
                    table.CheckConstraint("ck_messages_conversation_id_valid", "conversation_id IS NOT NULL");
                    table.CheckConstraint("ck_messages_role_valid", "role IN ('user', 'assistant', 'system', 'tool')");
                    table.CheckConstraint("ck_messages_sequence_valid", "sequence > 0");
                    table.ForeignKey(
                        name: "fk_fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "chat",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_created_at_utc",
                schema: "chat",
                table: "conversations",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_created_audit",
                schema: "chat",
                table: "conversations",
                columns: new[] { "created_by", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_status",
                schema: "chat",
                table: "conversations",
                column: "status",
                filter: "status IN ('Active', 'Completed')");

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_status_completed_at",
                schema: "chat",
                table: "conversations",
                columns: new[] { "status", "completed_at" },
                filter: "completed_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_title",
                schema: "chat",
                table: "conversations",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_updated_at_utc",
                schema: "chat",
                table: "conversations",
                column: "updated_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_user_id_created_at",
                schema: "chat",
                table: "conversations",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_conversations_user_id_status",
                schema: "chat",
                table: "conversations",
                columns: new[] { "user_id", "status" },
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_conversation_id",
                schema: "chat",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_conversation_id_created_at_utc",
                schema: "chat",
                table: "messages",
                columns: new[] { "conversation_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_conversation_id_role",
                schema: "chat",
                table: "messages",
                columns: new[] { "conversation_id", "role" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_conversation_id_sequence",
                schema: "chat",
                table: "messages",
                columns: new[] { "conversation_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_created_at_utc",
                schema: "chat",
                table: "messages",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_created_audit",
                schema: "chat",
                table: "messages",
                columns: new[] { "created_by", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_metadata_gin",
                schema: "chat",
                table: "messages",
                column: "metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_role",
                schema: "chat",
                table: "messages",
                column: "role",
                filter: "role IN ('user', 'assistant', 'system', 'tool')");

            migrationBuilder.CreateIndex(
                name: "ix_ix_messages_updated_at_utc",
                schema: "chat",
                table: "messages",
                column: "updated_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letter_messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "event_correlations",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "chat");
        }
    }
}
