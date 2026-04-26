using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantBilling.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveItemMasterLegacyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStockTracked",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SacCode",
                table: "Items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStockTracked",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "Items",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderLevel",
                table: "Items",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SacCode",
                table: "Items",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
