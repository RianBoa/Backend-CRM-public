using System;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023150000_CloseReviewRaces")]
public partial class CloseReviewRaces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("SecurityVersion", "users", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>("SecurityVersion", "refresh_tokens", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<Guid>("Version", "enrollments", type: "uuid", nullable: false, defaultValue: Guid.Empty);
        migrationBuilder.AddColumn<DateTimeOffset>("CanceledAtUtc", "integration_outbox", type: "timestamp with time zone", nullable: true);
        migrationBuilder.DropIndex("ix_integration_outbox_ready", "integration_outbox");
        migrationBuilder.CreateIndex("ix_integration_outbox_ready", "integration_outbox", new[] { "NextAttemptAtUtc", "CreatedAtUtc", "Id" },
            filter: "\"PublishedAtUtc\" IS NULL AND \"DeadLetteredAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not make stale sessions valid or restart canceled deliveries during a downgrade
        migrationBuilder.Sql("""
            UPDATE refresh_tokens AS r SET "RevokedAtUtc" = COALESCE(r."RevokedAtUtc", CURRENT_TIMESTAMP)
            FROM users AS u WHERE r."UserId" = u."Id" AND r."SecurityVersion" <> u."SecurityVersion";
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM integration_outbox WHERE "CanceledAtUtc" IS NOT NULL AND "PublishedAtUtc" IS NULL) THEN
                    RAISE EXCEPTION 'Reconcile canceled outbox messages before downgrading';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex("ix_integration_outbox_ready", "integration_outbox");
        migrationBuilder.CreateIndex("ix_integration_outbox_ready", "integration_outbox", new[] { "NextAttemptAtUtc", "CreatedAtUtc", "Id" },
            filter: "\"PublishedAtUtc\" IS NULL AND \"DeadLetteredAtUtc\" IS NULL");
        migrationBuilder.DropColumn("CanceledAtUtc", "integration_outbox");
        migrationBuilder.DropColumn("Version", "enrollments");
        migrationBuilder.DropColumn("SecurityVersion", "refresh_tokens");
        migrationBuilder.DropColumn("SecurityVersion", "users");
    }
}
