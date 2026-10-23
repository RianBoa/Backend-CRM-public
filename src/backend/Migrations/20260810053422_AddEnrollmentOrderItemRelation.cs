using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentOrderItemRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderItemId",
                table: "enrollments",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_OrderItemId",
                table: "enrollments",
                column: "OrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_enrollments_order_items_OrderItemId",
                table: "enrollments",
                column: "OrderItemId",
                principalTable: "order_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_enrollments_order_items_OrderItemId",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "IX_enrollments_OrderItemId",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "enrollments");
        }
    }
}
