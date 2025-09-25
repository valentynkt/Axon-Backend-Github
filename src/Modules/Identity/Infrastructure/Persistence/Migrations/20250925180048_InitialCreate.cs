using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Axon.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AxonUserAuth",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AxonPrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalIssuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DynamicEnvironmentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DynamicUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirstAuthenticatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAuthenticatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PrimaryChainId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrimaryWalletAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AxonUserAuth", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "identity",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "Principal",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RiskTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wallet",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallet", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "identity",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "identity",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "identity",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "Credential",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credential", x => x.id);
                    table.ForeignKey(
                        name: "FK_Credential_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalChainDefault",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalChainDefault", x => x.id);
                    table.ForeignKey(
                        name: "FK_PrincipalChainDefault_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletOwnership",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    AxonPrincipalId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletOwnership", x => x.id);
                    table.ForeignKey(
                        name: "FK_WalletOwnership_Principal_AxonPrincipalId",
                        column: x => x.AxonPrincipalId,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WalletOwnership_Principal_principal_id",
                        column: x => x.principal_id,
                        principalSchema: "identity",
                        principalTable: "Principal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletOwnership_Wallet_wallet_id",
                        column: x => x.wallet_id,
                        principalSchema: "identity",
                        principalTable: "Wallet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_AxonPrincipalId",
                schema: "identity",
                table: "AxonUserAuth",
                column: "AxonPrincipalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_DynamicUserId",
                schema: "identity",
                table: "AxonUserAuth",
                column: "DynamicUserId",
                filter: "\"DynamicUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_NormalizedEmail",
                schema: "identity",
                table: "AxonUserAuth",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_NormalizedUserName",
                schema: "identity",
                table: "AxonUserAuth",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_PrimaryWalletAddress",
                schema: "identity",
                table: "AxonUserAuth",
                column: "PrimaryWalletAddress",
                filter: "\"PrimaryWalletAddress\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AxonUserAuth_ProviderType_OriginalSubject",
                schema: "identity",
                table: "AxonUserAuth",
                columns: new[] { "ProviderType", "OriginalSubject" });

            migrationBuilder.CreateIndex(
                name: "idx_credential_principal_id",
                schema: "identity",
                table: "Credential",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "idx_credential_provider",
                schema: "identity",
                table: "Credential",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ux_credential_provider",
                schema: "identity",
                table: "Credential",
                columns: new[] { "provider", "issuer", "subject" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "identity",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "identity",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "identity",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "identity",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "identity",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "identity",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "ix_principal_created_at",
                schema: "identity",
                table: "Principal",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_principal_risk_tier",
                schema: "identity",
                table: "Principal",
                column: "RiskTier");

            migrationBuilder.CreateIndex(
                name: "ix_principal_type",
                schema: "identity",
                table: "Principal",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "idx_default_principal_id",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "principal_id");

            migrationBuilder.CreateIndex(
                name: "idx_default_wallet_id",
                schema: "identity",
                table: "PrincipalChainDefault",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ux_chain_default",
                schema: "identity",
                table: "PrincipalChainDefault",
                columns: new[] { "principal_id", "chain_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_wallet_chain_id",
                schema: "identity",
                table: "Wallet",
                column: "chain_id");

            migrationBuilder.CreateIndex(
                name: "idx_wallet_last_seen_at",
                schema: "identity",
                table: "Wallet",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ux_wallet_chain_addr",
                schema: "identity",
                table: "Wallet",
                columns: new[] { "chain_id", "address" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_principal_active",
                schema: "identity",
                table: "WalletOwnership",
                column: "principal_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_status",
                schema: "identity",
                table: "WalletOwnership",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_ownership_wallet_active",
                schema: "identity",
                table: "WalletOwnership",
                column: "wallet_id",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOwnership_AxonPrincipalId",
                schema: "identity",
                table: "WalletOwnership",
                column: "AxonPrincipalId");

            migrationBuilder.CreateIndex(
                name: "ux_ownership_pair",
                schema: "identity",
                table: "WalletOwnership",
                columns: new[] { "principal_id", "wallet_id" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AxonUserAuth",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Credential",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PrincipalChainDefault",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "WalletOwnership",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Principal",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Wallet",
                schema: "identity");
        }
    }
}
