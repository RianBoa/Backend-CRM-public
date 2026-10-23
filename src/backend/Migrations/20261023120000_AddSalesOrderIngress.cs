using backend.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023120000_AddSalesOrderIngress")]
public partial class AddSalesOrderIngress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Do not silently merge legacy identities when introducing the unique source key.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM external_accounts
                    GROUP BY lower(btrim("Provider")), btrim("ExternalId") HAVING count(*) > 1
                ) OR EXISTS (
                    SELECT 1 FROM external_accounts
                    GROUP BY "ClientId", lower(btrim("Provider")) HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Resolve ambiguous external account identities before applying sales ingress migration';
                END IF;
            END $$;
            UPDATE external_accounts SET "Provider" = lower(btrim("Provider")), "ExternalId" = btrim("ExternalId");
            """);
        migrationBuilder.CreateIndex("ux_external_accounts_provider_external_id", "external_accounts",
            new[] { "Provider", "ExternalId" }, unique: true);

        migrationBuilder.CreateTable("sales_order_links", columns: table => new
        {
            Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            DealId = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
            OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            ItemsFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_sales_order_links", x => new { x.Source, x.DealId });
            table.ForeignKey("FK_sales_order_links_orders_OrderId", x => x.OrderId, "orders", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_sales_order_links_OrderId", "sales_order_links", "OrderId", unique: true);

        migrationBuilder.CreateTable("sales_payment_links", columns: table => new
        {
            Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            PaymentId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_sales_payment_links", x => new { x.Source, x.ExternalId });
            table.ForeignKey("FK_sales_payment_links_payments_PaymentId", x => x.PaymentId, "payments", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_sales_payment_links_orders_OrderId", x => x.OrderId, "orders", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_sales_payment_links_PaymentId", "sales_payment_links", "PaymentId", unique: true);
        migrationBuilder.CreateIndex("IX_sales_payment_links_OrderId", "sales_payment_links", "OrderId");

        migrationBuilder.CreateTable("sales_import_receipts", columns: table => new
        {
            Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            EventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
            ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_sales_import_receipts", x => new { x.Source, x.EventId }));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("sales_payment_links");
        migrationBuilder.DropTable("sales_order_links");
        migrationBuilder.DropTable("sales_import_receipts");
        migrationBuilder.DropIndex("ux_external_accounts_provider_external_id", "external_accounts");
        // Provider canonicalization intentionally is not reversed.
    }
}
