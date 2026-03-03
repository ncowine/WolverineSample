using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Persistence.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class AddRuleDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleDiscoveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscoveredRulesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    PatternsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleDiscoveries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 2, 54, 74, DateTimeKind.Utc).AddTicks(4494));

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 18, 2, 54, 74, DateTimeKind.Utc).AddTicks(4518));

            migrationBuilder.CreateIndex(
                name: "IX_RuleDiscoveries_MarketCode",
                table: "RuleDiscoveries",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_RuleDiscoveries_StrategyId",
                table: "RuleDiscoveries",
                column: "StrategyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleDiscoveries");

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 17, 55, 47, 695, DateTimeKind.Utc).AddTicks(5801));

            migrationBuilder.UpdateData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 17, 55, 47, 695, DateTimeKind.Utc).AddTicks(5810));
        }
    }
}
