using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Backtest
{
    /// <inheritdoc />
    public partial class AddUniverseBacktestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UniverseBacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniverseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniverseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InitialCapital = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MaxPositions = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSymbols = table.Column<int>(type: "INTEGER", nullable: false),
                    SymbolsWithData = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniverseBacktestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniverseBacktestRuns_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UniverseBacktestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniverseBacktestRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    TotalReturn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Cagr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    SortinoRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    CalmarRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Expectancy = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UniqueSymbolsTraded = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePositionsHeld = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    MaxPositionsHeld = table.Column<int>(type: "INTEGER", nullable: false),
                    EquityCurveJson = table.Column<string>(type: "TEXT", maxLength: 500000, nullable: false),
                    TradeLogJson = table.Column<string>(type: "TEXT", maxLength: 500000, nullable: false),
                    MonthlyReturnsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    SymbolBreakdownJson = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    SpyComparisonJson = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniverseBacktestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniverseBacktestResults_UniverseBacktestRuns_UniverseBacktestRunId",
                        column: x => x.UniverseBacktestRunId,
                        principalTable: "UniverseBacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniverseBacktestResults_UniverseBacktestRunId",
                table: "UniverseBacktestResults",
                column: "UniverseBacktestRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniverseBacktestRuns_StrategyId",
                table: "UniverseBacktestRuns",
                column: "StrategyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UniverseBacktestResults");

            migrationBuilder.DropTable(
                name: "UniverseBacktestRuns");
        }
    }
}
