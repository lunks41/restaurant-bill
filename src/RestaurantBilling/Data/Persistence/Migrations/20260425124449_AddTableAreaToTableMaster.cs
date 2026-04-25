using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableAreaToTableMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "TableMasters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Ground");

            migrationBuilder.Sql(
                """
                UPDATE [TableMasters]
                SET [Area] =
                    CASE
                        WHEN LOWER([TableName]) LIKE '%non-ac%' OR LOWER([TableName]) LIKE '%non ac%' OR LOWER([TableName]) LIKE '%nonac%' THEN 'Non-AC'
                        WHEN LOWER([TableName]) LIKE '%outdoor%' OR LOWER([TableName]) LIKE '%outside%' OR LOWER([TableName]) LIKE '%patio%' THEN 'Outdoor'
                        WHEN LOWER([TableName]) LIKE '% ac%' OR LOWER([TableName]) LIKE 'ac %' OR LOWER([TableName]) LIKE '%(ac)%' OR LOWER([TableName]) LIKE '%-ac%' OR LOWER([TableName]) LIKE '%ac-%' THEN 'AC'
                        WHEN LOWER([TableName]) LIKE '%ground%' OR LOWER([TableName]) LIKE '%g-floor%' OR LOWER([TableName]) LIKE '%g floor%' THEN 'Ground'
                        ELSE [Area]
                    END
                WHERE [Area] = 'Ground';
                """);

            migrationBuilder.Sql(
                """
                UPDATE [TableMasters]
                SET [Area] = 'Ground'
                WHERE [Area] IS NULL OR [Area] = '';
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [Outlets] WHERE [OutletId] = 1)
                   AND NOT EXISTS (SELECT 1 FROM [TableMasters] WHERE [OutletId] = 1)
                BEGIN
                    INSERT INTO [TableMasters] ([OutletId], [TableName], [Area], [Capacity], [IsOccupied], [IsActive], [IsDeleted], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy], [DeletedAtUtc], [DeletedBy])
                    VALUES
                    (1, 'Ground-1', 'Ground', 4, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'Ground-2', 'Ground', 6, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'AC-1', 'AC', 2, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'AC-2', 'AC', 4, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'NonAC-1', 'Non-AC', 4, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'NonAC-2', 'Non-AC', 8, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'Outdoor-1', 'Outdoor', 2, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL),
                    (1, 'Outdoor-2', 'Outdoor', 6, 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "TableMasters");
        }
    }
}
