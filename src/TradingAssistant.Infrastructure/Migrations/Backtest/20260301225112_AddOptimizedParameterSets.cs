using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Backtest
{
    /// <inheritdoc />
    public partial class AddOptimizedParameterSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RulesJson",
                table: "Strategies",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BenchmarkReturnJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 500000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Cagr",
                table: "BacktestResults",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalmarRatio",
                table: "BacktestResults",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EquityCurveJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 500000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Expectancy",
                table: "BacktestResults",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MonthlyReturnsJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OverfittingScore",
                table: "BacktestResults",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParametersJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitFactor",
                table: "BacktestResults",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SortinoRatio",
                table: "BacktestResults",
                type: "TEXT",
                precision: 8,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SpyComparisonJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TradeLogJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 500000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalkForwardJson",
                table: "BacktestResults",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OptimizedParameterSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AvgOutOfSampleSharpe = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    AvgEfficiency = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    AvgOverfittingScore = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    OverfittingGrade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WindowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizedParameterSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptimizedParameterSets_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizedParameterSets_StrategyId_IsActive",
                table: "OptimizedParameterSets",
                columns: new[] { "StrategyId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptimizedParameterSets");

            migrationBuilder.DropColumn(
                name: "RulesJson",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "BenchmarkReturnJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "Cagr",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "CalmarRatio",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "EquityCurveJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "Expectancy",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "MonthlyReturnsJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "OverfittingScore",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "ParametersJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "ProfitFactor",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "SortinoRatio",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "SpyComparisonJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "TradeLogJson",
                table: "BacktestResults");

            migrationBuilder.DropColumn(
                name: "WalkForwardJson",
                table: "BacktestResults");
        }
    }
}
