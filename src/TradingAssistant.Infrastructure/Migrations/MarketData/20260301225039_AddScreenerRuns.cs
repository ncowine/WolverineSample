using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.MarketData
{
    /// <inheritdoc />
    public partial class AddScreenerRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceCandles_StockId_Timestamp_Interval",
                table: "PriceCandles");

            migrationBuilder.CreateTable(
                name: "ScreenerRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScanDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SymbolsScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    SignalsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", maxLength: 500000, nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ElapsedTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenerRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockUniverses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Symbols = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludesBenchmark = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockUniverses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_StockId_Interval_Timestamp",
                table: "PriceCandles",
                columns: new[] { "StockId", "Interval", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScreenerRuns_ScanDate",
                table: "ScreenerRuns",
                column: "ScanDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockUniverses_Name",
                table: "StockUniverses",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScreenerRuns");

            migrationBuilder.DropTable(
                name: "StockUniverses");

            migrationBuilder.DropIndex(
                name: "IX_PriceCandles_StockId_Interval_Timestamp",
                table: "PriceCandles");

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_StockId_Timestamp_Interval",
                table: "PriceCandles",
                columns: new[] { "StockId", "Timestamp", "Interval" });
        }
    }
}
