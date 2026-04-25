using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantBilling.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillTableAreaAndSeedMissingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent data fix: normalize Area, infer from table names, add missing demo rows for outlet 1.
            migrationBuilder.Sql(
                """
                -- Normalize blank / whitespace Area to Ground
                UPDATE [TableMasters]
                SET [Area] = N'Ground'
                WHERE [Area] IS NULL
                   OR LTRIM(RTRIM([Area])) = N'';

                -- Infer Area from table name when value is still default Ground (same rules as AddTableAreaToTableMaster)
                UPDATE [TableMasters]
                SET [Area] =
                    CASE
                        WHEN LOWER([TableName]) LIKE N'%non-ac%' OR LOWER([TableName]) LIKE N'%non ac%' OR LOWER([TableName]) LIKE N'%nonac%' THEN N'Non-AC'
                        WHEN LOWER([TableName]) LIKE N'%outdoor%' OR LOWER([TableName]) LIKE N'%outside%' OR LOWER([TableName]) LIKE N'%patio%' THEN N'Outdoor'
                        WHEN LOWER([TableName]) LIKE N'% ac%' OR LOWER([TableName]) LIKE N'ac %' OR LOWER([TableName]) LIKE N'%(ac)%' OR LOWER([TableName]) LIKE N'%-ac%' OR LOWER([TableName]) LIKE N'%ac-%' THEN N'AC'
                        WHEN LOWER([TableName]) LIKE N'%ground%' OR LOWER([TableName]) LIKE N'%g-floor%' OR LOWER([TableName]) LIKE N'%g floor%' THEN N'Ground'
                        ELSE [Area]
                    END
                WHERE [Area] = N'Ground';

                UPDATE [TableMasters]
                SET [Area] = N'Ground'
                WHERE [Area] IS NULL
                   OR LTRIM(RTRIM([Area])) = N'';

                -- Seed default sample tables for outlet 1 when each name is missing (non-deleted rows only)
                IF EXISTS (SELECT 1 FROM [Outlets] WHERE [OutletId] = 1)
                BEGIN
                    INSERT INTO [TableMasters] ([OutletId], [TableName], [Area], [Capacity], [IsOccupied], [IsActive], [IsDeleted], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy], [DeletedAtUtc], [DeletedBy])
                    SELECT v.[OutletId], v.[TableName], v.[Area], v.[Capacity], 0, 1, 0, SYSUTCDATETIME(), 0, NULL, NULL, NULL, NULL
                    FROM (VALUES
                        (1, N'Ground-1', N'Ground', 4),
                        (1, N'Ground-2', N'Ground', 6),
                        (1, N'AC-1', N'AC', 2),
                        (1, N'AC-2', N'AC', 4),
                        (1, N'NonAC-1', N'Non-AC', 4),
                        (1, N'NonAC-2', N'Non-AC', 8),
                        (1, N'Outdoor-1', N'Outdoor', 2),
                        (1, N'Outdoor-2', N'Outdoor', 6)
                    ) AS v([OutletId], [TableName], [Area], [Capacity])
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [TableMasters] t
                        WHERE t.[OutletId] = v.[OutletId]
                          AND t.[TableName] = v.[TableName]
                          AND t.[IsDeleted] = 0
                    );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; no schema to revert.
        }
    }
}
