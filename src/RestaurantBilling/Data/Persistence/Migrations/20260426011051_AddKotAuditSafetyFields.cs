using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantBilling.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKotAuditSafetyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KotPrintedAtUtc",
                table: "KotHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServedAtUtc",
                table: "KotHeaders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServedByUserId",
                table: "KotHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KotPrintedAtUtc",
                table: "KotHeaders");

            migrationBuilder.DropColumn(
                name: "ServedAtUtc",
                table: "KotHeaders");

            migrationBuilder.DropColumn(
                name: "ServedByUserId",
                table: "KotHeaders");
        }
    }
}
