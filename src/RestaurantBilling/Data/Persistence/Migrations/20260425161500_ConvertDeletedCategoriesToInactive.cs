using Data.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260425161500_ConvertDeletedCategoriesToInactive")]
    public class ConvertDeletedCategoriesToInactive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Categories]
                SET [IsActive] = CAST(0 AS bit),
                    [IsDeleted] = CAST(0 AS bit),
                    [DeletedAtUtc] = NULL
                WHERE [IsDeleted] = CAST(1 AS bit);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Categories]
                SET [IsDeleted] = CAST(1 AS bit),
                    [DeletedAtUtc] = SYSUTCDATETIME()
                WHERE [IsActive] = CAST(0 AS bit) AND [IsDeleted] = CAST(0 AS bit);
                """);
        }
    }
}
