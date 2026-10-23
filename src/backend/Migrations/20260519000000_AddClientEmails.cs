using System;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260519000000_AddClientEmails")]
    public partial class AddClientEmails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_email",
                table: "clients");

            migrationBuilder.CreateTable(
                name: "client_emails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    client_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_emails_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO client_emails ("Id", client_id, email, is_primary, created_at_utc)
                SELECT
                    substring("Id", 1, 20) || 'email1',
                    "Id",
                    email,
                    true,
                    created_at_utc
                FROM clients
                WHERE email IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "email",
                table: "clients");

            migrationBuilder.CreateIndex(
                name: "ix_client_emails_client_id_email",
                table: "client_emails",
                columns: new[] { "client_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_emails_email",
                table: "client_emails",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_client_emails_primary_per_client",
                table: "client_emails",
                columns: new[] { "client_id", "is_primary" },
                unique: true,
                filter: "\"is_primary\" = true");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "clients",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE clients c
                SET email = ce.email
                FROM client_emails ce
                WHERE ce.client_id = c."Id"
                  AND ce.is_primary = true;
                """);

            migrationBuilder.DropTable(
                name: "client_emails");

            migrationBuilder.CreateIndex(
                name: "ix_clients_email",
                table: "clients",
                column: "email");
        }
    }
}
