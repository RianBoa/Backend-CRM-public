using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAccessLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "start_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                table: "subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provisioning_key",
                table: "subscriptions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscription_provisioning_key",
                table: "subscriptions",
                column: "provisioning_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM subscriptions WHERE start_at IS NULL OR end_at IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back while subscriptions await access. Resolve them before downgrading.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "ux_subscription_provisioning_key",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "duration_days",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "provisioning_key",
                table: "subscriptions");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_at",
                table: "subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
