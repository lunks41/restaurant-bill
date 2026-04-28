using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Persistence.Migrations
{
    public partial class ItemStockFromGroceryStock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GroceryStockItems",
                table: "GroceryStockItems");

            migrationBuilder.RenameTable(
                name: "GroceryStockItems",
                newName: "ItemStock");

            migrationBuilder.RenameColumn(
                name: "GroceryStockItemId",
                table: "ItemStock",
                newName: "ItemStockId");

            migrationBuilder.RenameColumn(
                name: "GroceryId",
                table: "ItemStock",
                newName: "ItemId");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StockDate",
                table: "ItemStock",
                type: "date",
                nullable: false,
                defaultValueSql: "CAST(GETDATE() AS date)");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ItemStock",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Opening");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemStock",
                table: "ItemStock",
                column: "ItemStockId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemStock",
                table: "ItemStock");

            migrationBuilder.DropColumn(
                name: "StockDate",
                table: "ItemStock");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ItemStock");

            migrationBuilder.RenameTable(
                name: "ItemStock",
                newName: "GroceryStockItems");

            migrationBuilder.RenameColumn(
                name: "ItemStockId",
                table: "GroceryStockItems",
                newName: "GroceryStockItemId");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "GroceryStockItems",
                newName: "GroceryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroceryStockItems",
                table: "GroceryStockItems",
                column: "GroceryStockItemId");
        }
    }
}
