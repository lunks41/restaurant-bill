using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantBilling.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableNameToBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TableName",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TableName",
                table: "Bills");
        }
    }
}
