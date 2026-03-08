using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Backtest
{
    /// <inheritdoc />
    public partial class MergeUniverseIntoBacktest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UniverseBacktestResults");

            migrationBuilder.DropTable(
                name: "UniverseBacktestRuns");

            migrationBuilder.AddColumn<decimal>(
                name: "InitialCapital",
                table: "BacktestRuns",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaxPositions",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SymbolsWithData",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalSymbols",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UniverseId",
                table: "BacktestRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniverseName",
                table: "BacktestRuns",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AveragePositionsHeld",
                table: "BacktestResults",
                type: "TEXT",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionLogJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 500000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPositionsHeld",
                table: "BacktestResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegimeTimelineJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 50000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SymbolBreakdownJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 100000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UniqueSymbolsTraded",
                table: "BacktestResults",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialCapital",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "MaxPositions",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "SymbolsWithData",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "TotalSymbols",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "UniverseId",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "UniverseName",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "AveragePositionsHeld",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "ExecutionLogJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "MaxPositionsHeld",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "RegimeTimelineJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "SymbolBreakdownJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "UniqueSymbolsTraded",
                table: "BacktestResults");

            migrationBuilder.CreateTable(
                name: "UniverseBacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InitialCapital = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MaxPositions = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SymbolsWithData = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSymbols = table.Column<int>(type: "INTEGER", nullable: false),
                    UniverseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniverseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
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
                    AveragePositionsHeld = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    Cagr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CalmarRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EquityCurveJson = table.Column<string>(type: "TEXT", maxLength: 500000, nullable: false),
                    Expectancy = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MaxPositionsHeld = table.Column<int>(type: "INTEGER", nullable: false),
                    MonthlyReturnsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SortinoRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SpyComparisonJson = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    SymbolBreakdownJson = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    TotalReturn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    TradeLogJson = table.Column<string>(type: "TEXT", maxLength: 500000, nullable: false),
                    UniqueSymbolsTraded = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false)
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
    }
}
