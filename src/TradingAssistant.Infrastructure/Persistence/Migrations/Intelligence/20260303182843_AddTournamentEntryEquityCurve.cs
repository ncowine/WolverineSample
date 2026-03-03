using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Persistence.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class AddTournamentEntryEquityCurve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquityCurveJson",
                table: "TournamentEntries",
                type: "TEXT",
                maxLength: 16000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StrategyName",
                table: "TournamentEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 28, 43, 285, DateTimeKind.Utc).AddTicks(4420));

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 28, 43, 285, DateTimeKind.Utc).AddTicks(4429));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquityCurveJson",
                table: "TournamentEntries");

            migrationBuilder.DropColumn(
                name: "StrategyName",
                table: "TournamentEntries");

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 19, 58, 750, DateTimeKind.Utc).AddTicks(6563));

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 19, 58, 750, DateTimeKind.Utc).AddTicks(6582));
        }
    }
}
