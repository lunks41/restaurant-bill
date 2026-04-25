using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemImageAndInactiveSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Items",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Items]
                SET [IsActive] = CAST(0 AS bit),
                    [IsDeleted] = CAST(0 AS bit),
                    [DeletedAtUtc] = NULL
                WHERE [IsDeleted] = CAST(1 AS bit);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Items");
        }
    }
}
