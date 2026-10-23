using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_types",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SlaDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    telegram = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    discord = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Contacts_Instagram = table.Column<string>(type: "text", nullable: true),
                    initial_budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    total_earnings = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occupation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "note_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    legacy_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    discord_access = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TelegramWork = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_accounts",
                columns: table => new
                {
                    Ulid = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_accounts", x => x.Ulid);
                    table.ForeignKey(
                        name: "FK_external_accounts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    client_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cohorts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cohorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cohorts_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    discord_access = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    discord_days = table.Column<int>(type: "integer", nullable: true),
                    product_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_packages_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roadmap_templates_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    ScreenshotUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TelegramUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HasConsent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "New"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedById = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClientId1 = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cases_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cases_clients_ClientId1",
                        column: x => x.ClientId1,
                        principalTable: "clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cases_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curator_notes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curator_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_curator_notes_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_curator_notes_note_types_type_id",
                        column: x => x.type_id,
                        principalTable: "note_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curator_notes_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    type_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    author_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    client_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    order_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_by_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_applications_application_types_type_id",
                        column: x => x.type_id,
                        principalTable: "application_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_applications_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_applications_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_users_processed_by_id",
                        column: x => x.processed_by_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    order_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount_ua = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    invoice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_installments = table.Column<int>(type: "integer", nullable: false),
                    current_installment = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CohortId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    MentorId = table.Column<int>(type: "integer", nullable: true),
                    PackageId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LearningPlatformId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_groups_cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "cohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_groups_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    order_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    package_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    meta = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "promo_benefit_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    package_id = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    discount_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    discount_fixed_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    effect_days = table.Column<int>(type: "integer", nullable: true),
                    bonus_product_id = table.Column<int>(type: "integer", nullable: true),
                    bonus_package_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_benefit_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promo_benefit_rules_packages_bonus_package_id",
                        column: x => x.bonus_package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promo_benefit_rules_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promo_benefit_rules_products_bonus_product_id",
                        column: x => x.bonus_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_promo_benefit_rules_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    roadmap_template_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    days_from_start = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roadmap_items_roadmap_templates_roadmap_template_id",
                        column: x => x.roadmap_template_id,
                        principalTable: "roadmap_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    CohortId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    GroupId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    IsAddedToChat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AddedToChatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAddedToPlatform = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    AddedToPlatformAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DesiredStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_enrollments_cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "cohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_enrollments_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_enrollments_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condition_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ConditionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condition_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_condition_rules_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_condition_rules_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    client_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    order_item_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applied_benefits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OrderItemId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    PromoBenefitRuleId = table.Column<int>(type: "integer", nullable: false),
                    OrderId1 = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    PromoBenefitRuleId1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applied_benefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_applied_benefits_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_applied_benefits_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_applied_benefits_orders_OrderId1",
                        column: x => x.OrderId1,
                        principalTable: "orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_applied_benefits_promo_benefit_rules_PromoBenefitRuleId",
                        column: x => x.PromoBenefitRuleId,
                        principalTable: "promo_benefit_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applied_benefits_promo_benefit_rules_PromoBenefitRuleId1",
                        column: x => x.PromoBenefitRuleId1,
                        principalTable: "promo_benefit_rules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "roadmap_assignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    roadmap_item_id = table.Column<int>(type: "integer", nullable: false),
                    enrollment_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    due_at_days_from_enrollment = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roadmap_assignments_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roadmap_assignments_roadmap_items_roadmap_item_id",
                        column: x => x.roadmap_item_id,
                        principalTable: "roadmap_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roadmap_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discord_accesses",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    subscription_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubscriptionId1 = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    discord_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_accesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_discord_accesses_subscriptions_SubscriptionId1",
                        column: x => x.SubscriptionId1,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_discord_accesses_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_accesses",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    subscription_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubscriptionId1 = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    platform_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    platform_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_accesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_platform_accesses_subscriptions_SubscriptionId1",
                        column: x => x.SubscriptionId1,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_platform_accesses_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_adjustments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    subscription_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    SubscriptionId1 = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    adjustment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    days_changed = table.Column<int>(type: "integer", nullable: true),
                    old_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    new_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    new_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    related_order_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_adjustments_subscriptions_SubscriptionId1",
                        column: x => x.SubscriptionId1,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscription_adjustments_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubscriptionId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Open"),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tasks_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_users_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_types_Code",
                table: "application_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_types_IsActive",
                table: "application_types",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_application_types_TargetRole",
                table: "application_types",
                column: "TargetRole");

            migrationBuilder.CreateIndex(
                name: "IX_applications_author_id",
                table: "applications",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_client_id",
                table: "applications",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_order_id",
                table: "applications",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_processed_by_id",
                table: "applications",
                column: "processed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_reason",
                table: "applications",
                column: "reason");

            migrationBuilder.CreateIndex(
                name: "IX_applications_status",
                table: "applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_applications_type_id",
                table: "applications",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "IX_applied_benefits_OrderId",
                table: "applied_benefits",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_applied_benefits_OrderId1",
                table: "applied_benefits",
                column: "OrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_applied_benefits_OrderItemId",
                table: "applied_benefits",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_applied_benefits_PromoBenefitRuleId",
                table: "applied_benefits",
                column: "PromoBenefitRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_applied_benefits_PromoBenefitRuleId1",
                table: "applied_benefits",
                column: "PromoBenefitRuleId1");

            migrationBuilder.CreateIndex(
                name: "IX_cases_ClientId",
                table: "cases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_cases_ClientId1",
                table: "cases",
                column: "ClientId1");

            migrationBuilder.CreateIndex(
                name: "IX_cases_CreatedAt",
                table: "cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_cases_CreatedById",
                table: "cases",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_cases_Status",
                table: "cases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_cases_Type",
                table: "cases",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_clients_discord",
                table: "clients",
                column: "discord");

            migrationBuilder.CreateIndex(
                name: "ix_clients_email",
                table: "clients",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_clients_full_name",
                table: "clients",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "ix_clients_phone",
                table: "clients",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_clients_telegram",
                table: "clients",
                column: "telegram");

            migrationBuilder.CreateIndex(
                name: "IX_cohorts_ProductId",
                table: "cohorts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_cohorts_StartAt",
                table: "cohorts",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_cohorts_Status",
                table: "cohorts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_condition_rules_OrderItemId",
                table: "condition_rules",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_condition_rules_ProductId",
                table: "condition_rules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_condition_rules_UpdatedAt",
                table: "condition_rules",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_curator_notes_client_id",
                table: "curator_notes",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_curator_notes_created_by_id",
                table: "curator_notes",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_curator_notes_type_id",
                table: "curator_notes",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "IX_discord_accesses_discord_user_id",
                table: "discord_accesses",
                column: "discord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_discord_accesses_subscription_id",
                table: "discord_accesses",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_discord_accesses_SubscriptionId1",
                table: "discord_accesses",
                column: "SubscriptionId1");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_CohortId",
                table: "enrollments",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_EnrolledAt",
                table: "enrollments",
                column: "EnrolledAt");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_GroupId",
                table: "enrollments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_IsAddedToChat",
                table: "enrollments",
                column: "IsAddedToChat");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_IsAddedToPlatform",
                table: "enrollments",
                column: "IsAddedToPlatform");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_OrderId",
                table: "enrollments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_Status",
                table: "enrollments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_external_accounts_ClientId_Provider",
                table: "external_accounts",
                columns: new[] { "ClientId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_accounts_ExternalId",
                table: "external_accounts",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_CohortId",
                table: "groups",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_MentorId",
                table: "groups",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_Name",
                table: "groups",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_groups_PackageId",
                table: "groups",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_note_types_Category",
                table: "note_types",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_note_types_Name",
                table: "note_types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_package_id",
                table: "order_items",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_client_id",
                table: "orders",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_packages_code",
                table: "packages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_packages_product_id",
                table: "packages",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_order_id",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_paid_at",
                table: "payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "IX_payments_status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_accesses_platform_user_id",
                table: "platform_accesses",
                column: "platform_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_accesses_subscription_id",
                table: "platform_accesses",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_accesses_SubscriptionId1",
                table: "platform_accesses",
                column: "SubscriptionId1");

            migrationBuilder.CreateIndex(
                name: "IX_products_code",
                table: "products",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_legacy_code",
                table: "products",
                column: "legacy_code");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_bonus_package_id",
                table: "promo_benefit_rules",
                column: "bonus_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_bonus_product_id",
                table: "promo_benefit_rules",
                column: "bonus_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_is_active",
                table: "promo_benefit_rules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_package_id",
                table: "promo_benefit_rules",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_product_id",
                table: "promo_benefit_rules",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_promo_benefit_rules_rule_code",
                table: "promo_benefit_rules",
                column: "rule_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_start_at",
                table: "promo_benefit_rules",
                column: "start_at");

            migrationBuilder.CreateIndex(
                name: "IX_promo_benefit_rules_type",
                table: "promo_benefit_rules",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_assignments_enrollment_id",
                table: "roadmap_assignments",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_assignments_roadmap_item_id",
                table: "roadmap_assignments",
                column: "roadmap_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_assignments_status",
                table: "roadmap_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_assignments_user_id",
                table: "roadmap_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_items_days_from_start",
                table: "roadmap_items",
                column: "days_from_start");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_items_roadmap_template_id",
                table: "roadmap_items",
                column: "roadmap_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_items_type",
                table: "roadmap_items",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_templates_is_active",
                table: "roadmap_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_templates_name",
                table: "roadmap_templates",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_templates_product_id",
                table: "roadmap_templates",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_adjustments_source",
                table: "subscription_adjustments",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_adjustments_subscription_id",
                table: "subscription_adjustments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_adjustments_SubscriptionId1",
                table: "subscription_adjustments",
                column: "SubscriptionId1");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_adjustments_type",
                table: "subscription_adjustments",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_client_id",
                table: "subscriptions",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_order_item_id",
                table: "subscriptions",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_ProductId",
                table: "subscriptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Status",
                table: "subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_AssignedTo",
                table: "tasks",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ClientId",
                table: "tasks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Priority",
                table: "tasks",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status",
                table: "tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_SubscriptionId",
                table: "tasks",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Type",
                table: "tasks",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_users_CreatedAt",
                table: "users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_IsActive",
                table: "users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_users_Role",
                table: "users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_users_TelegramUsername",
                table: "users",
                column: "TelegramUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "applied_benefits");

            migrationBuilder.DropTable(
                name: "cases");

            migrationBuilder.DropTable(
                name: "condition_rules");

            migrationBuilder.DropTable(
                name: "curator_notes");

            migrationBuilder.DropTable(
                name: "discord_accesses");

            migrationBuilder.DropTable(
                name: "external_accounts");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "platform_accesses");

            migrationBuilder.DropTable(
                name: "roadmap_assignments");

            migrationBuilder.DropTable(
                name: "subscription_adjustments");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "application_types");

            migrationBuilder.DropTable(
                name: "promo_benefit_rules");

            migrationBuilder.DropTable(
                name: "note_types");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "roadmap_items");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "roadmap_templates");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "cohorts");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "packages");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
