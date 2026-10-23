using System;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023140000_AddBotMessaging")]
public partial class AddBotMessaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("discord_grant_requests", columns: table => new
        {
            Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            SubscriptionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            ClientId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            ActiveSubscriptionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
            RequestedById = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            DiscordUserId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            GuildId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            RoleId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            RoleName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            CorrelationId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            ResultFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_discord_grant_requests", x => x.Id);
            table.ForeignKey("FK_discord_grant_requests_subscriptions_SubscriptionId", x => x.SubscriptionId,
                "subscriptions", "id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("integration_outbox", columns: table => new
        {
            Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            CorrelationId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            MessageType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Attempts = table.Column<int>(type: "integer", nullable: false),
            PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_integration_outbox", x => x.Id);
            table.ForeignKey("FK_integration_outbox_discord_grant_requests_Id", x => x.Id,
                "discord_grant_requests", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("integration_inbox", columns: table => new
        {
            Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            RequestId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_integration_inbox", x => x.Id);
            table.ForeignKey("FK_integration_inbox_discord_grant_requests_RequestId", x => x.RequestId,
                "discord_grant_requests", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_discord_grant_requests_ActiveSubscriptionId", "discord_grant_requests", "ActiveSubscriptionId", unique: true);
        migrationBuilder.CreateIndex("IX_discord_grant_requests_SubscriptionId_RequestedAtUtc", "discord_grant_requests", new[] { "SubscriptionId", "RequestedAtUtc" });
        migrationBuilder.CreateIndex("IX_integration_inbox_RequestId", "integration_inbox", "RequestId");
        migrationBuilder.CreateIndex("ix_integration_outbox_ready", "integration_outbox", new[] { "NextAttemptAtUtc", "CreatedAtUtc", "Id" },
            filter: "\"PublishedAtUtc\" IS NULL AND \"DeadLetteredAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("integration_inbox");
        migrationBuilder.DropTable("integration_outbox");
        migrationBuilder.DropTable("discord_grant_requests");
    }
}
