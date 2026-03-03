using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradingAssistant.Infrastructure.Persistence.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class AddStrategyAutopsy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CircuitBreakerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeakEquity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentEquity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DrawdownPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    ThresholdPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    RegimeAtEvent = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    RegimeConfidence = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: true),
                    PendingOrdersCancelled = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenPositionsAtEvent = table.Column<int>(type: "INTEGER", nullable: false),
                    DeactivationReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircuitBreakerEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Regime = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    SwitchoverStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyAutopsies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MonthlyReturnPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    MaxDrawdownPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    WinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryLossReason = table.Column<int>(type: "INTEGER", nullable: false),
                    RootCausesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    MarketConditionImpact = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RecommendationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ShouldRetire = table.Column<bool>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyAutopsies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyRegimeScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Regime = table.Column<int>(type: "INTEGER", nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SampleSize = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyRegimeScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TournamentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaperAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DaysActive = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SharpeRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    TotalReturn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PromotedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    RetirementReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CostProfiles",
                columns: new[] { "Id", "CommissionPerShare", "CommissionPercent", "CreatedAt", "ExchangeFeePercent", "IsActive", "MarketCode", "Name", "SpreadEstimatePercent", "TaxPercent", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b2c3d4e5-0001-0001-0001-000000000001"), 0.005m, 0m, new DateTime(2026, 3, 3, 17, 55, 47, 695, DateTimeKind.Utc).AddTicks(5801), 0m, true, "US_SP500", "US Equities (default)", 0.1m, 0m, null },
                    { new Guid("b2c3d4e5-0001-0001-0001-000000000002"), 0m, 0.03m, new DateTime(2026, 3, 3, 17, 55, 47, 695, DateTimeKind.Utc).AddTicks(5810), 0m, true, "IN_NIFTY50", "India Equities (default)", 0.05m, 0.025m, null }
                });

            migrationBuilder.InsertData(
                table: "MarketProfiles",
                columns: new[] { "Id", "ConfigJson", "CreatedAt", "Currency", "DataSource", "DnaProfileJson", "DnaProfileUpdatedAt", "Exchange", "IsActive", "MarketCode", "Timezone", "UpdatedAt", "VixSymbol" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000001"), "{\"tradingHours\":{\"open\":\"09:30\",\"close\":\"16:00\"},\"regimeThresholds\":{\"highVol\":30,\"bullBreadth\":0.60,\"bearBreadth\":0.40}}", new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "yahoo", "{}", null, "NYSE/NASDAQ", true, "US_SP500", "America/New_York", null, "^VIX" },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000002"), "{\"tradingHours\":{\"open\":\"09:15\",\"close\":\"15:30\"},\"regimeThresholds\":{\"highVol\":25,\"bullBreadth\":0.55,\"bearBreadth\":0.45}}", new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Utc), "INR", "yahoo", "{}", null, "NSE", true, "IN_NIFTY50", "Asia/Kolkata", null, "^INDIAVIX" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CircuitBreakerEvents_AccountId_EventDate",
                table: "CircuitBreakerEvents",
                columns: new[] { "AccountId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyAssignments_MarketCode",
                table: "StrategyAssignments",
                column: "MarketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategyAutopsies_MarketCode_PeriodStart",
                table: "StrategyAutopsies",
                columns: new[] { "MarketCode", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyAutopsies_StrategyId",
                table: "StrategyAutopsies",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyRegimeScores_StrategyId_MarketCode_Regime",
                table: "StrategyRegimeScores",
                columns: new[] { "StrategyId", "MarketCode", "Regime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntries_Status_MarketCode",
                table: "TournamentEntries",
                columns: new[] { "Status", "MarketCode" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntries_StrategyId",
                table: "TournamentEntries",
                column: "StrategyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircuitBreakerEvents");

            migrationBuilder.DropTable(
                name: "StrategyAssignments");

            migrationBuilder.DropTable(
                name: "StrategyAutopsies");

            migrationBuilder.DropTable(
                name: "StrategyRegimeScores");

            migrationBuilder.DropTable(
                name: "TournamentEntries");

            migrationBuilder.DeleteData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000001"));

            migrationBuilder.DeleteData(
                table: "CostProfiles",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-0001-0001-0001-000000000002"));

            migrationBuilder.DeleteData(
                table: "MarketProfiles",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"));

            migrationBuilder.DeleteData(
                table: "MarketProfiles",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000002"));
        }
    }
}
