using backend.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023130000_FixDuplicateRelationships")]
public partial class FixDuplicateRelationships : Migration
{
    private static readonly (string Table, string Principal, string PrincipalKey,
        string Key, string Shadow, bool Required, bool Integer)[] Links =
    [
        ("cases", "clients", "Id", "ClientId", "ClientId1", false, false),
        ("applied_benefits", "orders", "Id", "OrderId", "OrderId1", false, false),
        ("applied_benefits", "promo_benefit_rules", "Id", "PromoBenefitRuleId", "PromoBenefitRuleId1", false, true),
        ("discord_accesses", "subscriptions", "id", "subscription_id", "SubscriptionId1", true, false),
        ("platform_accesses", "subscriptions", "id", "subscription_id", "SubscriptionId1", true, false),
        ("subscription_adjustments", "subscriptions", "id", "subscription_id", "SubscriptionId1", true, false)
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep validation and schema changes in one transaction, without concurrent writes.
        migrationBuilder.Sql("""
            LOCK TABLE cases, applied_benefits, discord_accesses,
                platform_accesses, subscription_adjustments IN ACCESS EXCLUSIVE MODE;
            """);

        foreach (var link in Links)
        {
            // Identifiers come only from the constant mapping above, never request input.
            migrationBuilder.Sql($"""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "{link.Table}"
                        WHERE "{link.Shadow}" IS NOT NULL
                          AND "{link.Shadow}" IS DISTINCT FROM "{link.Key}"
                    ) THEN
                        RAISE EXCEPTION 'Conflicting {link.Table}.{link.Shadow}; reconcile with {link.Key} before retrying';
                    END IF;
                END $$;
                """);
        }

        foreach (var link in Links)
        {
            migrationBuilder.DropForeignKey($"FK_{link.Table}_{link.Principal}_{link.Shadow}", link.Table);
            migrationBuilder.DropIndex($"IX_{link.Table}_{link.Shadow}", link.Table);
            migrationBuilder.DropColumn(link.Shadow, link.Table);
        }

        migrationBuilder.DropForeignKey("FK_enrollments_groups_GroupId", "enrollments");
        migrationBuilder.AddForeignKey("FK_enrollments_groups_GroupId", "enrollments", "GroupId",
            "groups", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_enrollments_groups_GroupId", "enrollments");
        migrationBuilder.AddForeignKey("FK_enrollments_groups_GroupId", "enrollments", "GroupId",
            "groups", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

        // Restore the old schema with consistent keys, not the historical shadow/null values.
        foreach (var link in Links)
        {
            if (link.Integer)
                migrationBuilder.AddColumn<int>(link.Shadow, link.Table, type: "integer", nullable: true);
            else
                migrationBuilder.AddColumn<string>(link.Shadow, link.Table,
                    type: "character varying(26)", maxLength: 26, nullable: true);

            migrationBuilder.Sql($"UPDATE \"{link.Table}\" SET \"{link.Shadow}\" = \"{link.Key}\";");

            if (link.Required)
                migrationBuilder.AlterColumn<string>(link.Shadow, link.Table,
                    type: "character varying(26)", maxLength: 26, nullable: false,
                    oldClrType: typeof(string), oldType: "character varying(26)",
                    oldMaxLength: 26, oldNullable: true);

            migrationBuilder.CreateIndex($"IX_{link.Table}_{link.Shadow}", link.Table, link.Shadow);
            migrationBuilder.AddForeignKey($"FK_{link.Table}_{link.Principal}_{link.Shadow}",
                link.Table, link.Shadow, link.Principal, principalColumn: link.PrincipalKey,
                onDelete: link.Required ? ReferentialAction.Cascade : ReferentialAction.NoAction);
        }
    }
}
