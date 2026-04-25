using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertDeletedUnitsAndTablesToInactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Units]
                SET [IsActive] = CAST(0 AS bit),
                    [IsDeleted] = CAST(0 AS bit),
                    [DeletedAtUtc] = NULL
                WHERE [IsDeleted] = CAST(1 AS bit);
                """);

            migrationBuilder.Sql("""
                UPDATE [TableMasters]
                SET [IsActive] = CAST(0 AS bit),
                    [IsDeleted] = CAST(0 AS bit),
                    [DeletedAtUtc] = NULL
                WHERE [IsDeleted] = CAST(1 AS bit);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
