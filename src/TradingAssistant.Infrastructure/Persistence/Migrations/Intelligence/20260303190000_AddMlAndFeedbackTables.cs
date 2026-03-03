using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Persistence.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class AddMlAndFeedbackTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnsembleSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SignalDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    VotingMode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MinAgreement = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalVoters = table.Column<int>(type: "INTEGER", nullable: false),
                    AgreeingVoters = table.Column<int>(type: "INTEGER", nullable: false),
                    VotesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnsembleSignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FeatureVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    FeatureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FeaturesJson = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                    FeaturesHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TradeOutcome = table.Column<int>(type: "INTEGER", nullable: false),
                    TradePnlPercent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    OutcomeUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MlModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ModelVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    FeatureVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TrainedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Auc = table.Column<double>(type: "REAL", nullable: false),
                    Precision = table.Column<double>(type: "REAL", nullable: false),
                    Recall = table.Column<double>(type: "REAL", nullable: false),
                    F1Score = table.Column<double>(type: "REAL", nullable: false),
                    Accuracy = table.Column<double>(type: "REAL", nullable: false),
                    TrainingSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidationSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    WinSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    LossSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    FeatureImportanceJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeactivationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EntryPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ExitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExitDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PnlPercent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PnlAbsolute = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DurationHours = table.Column<double>(type: "REAL", nullable: false),
                    RegimeAtEntry = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    RegimeAtExit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Grade = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    MlConfidence = table.Column<float>(type: "REAL", nullable: true),
                    IndicatorValuesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    OutcomeClass = table.Column<int>(type: "INTEGER", nullable: false),
                    MistakeType = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    WeaknessesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    LessonsLearnedJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyDecayAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AlertType = table.Column<int>(type: "INTEGER", nullable: false),
                    Rolling30DaySharpe = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling60DaySharpe = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling90DaySharpe = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling30DayWinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling60DayWinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling90DayWinRate = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    Rolling30DayAvgPnl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Rolling60DayAvgPnl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Rolling90DayAvgPnl = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    HistoricalSharpe = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ClaudeAnalysis = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AlertedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyDecayAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MistakePatternReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MostCommonMistake = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MistakeBreakdownJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RegimeBreakdownJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RecommendationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ClaudeAnalysis = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MistakePatternReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalReturn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Alpha = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    BetaContribution = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    RegimeContribution = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Residual = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Beta = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    BenchmarkReturn = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RegimeAlignedTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    RegimeMismatchedTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyAttributions", x => x.Id);
                });

            // Indexes for EnsembleSignals
            migrationBuilder.CreateIndex(
                name: "IX_EnsembleSignals_MarketCode_Symbol_SignalDate",
                table: "EnsembleSignals",
                columns: new[] { "MarketCode", "Symbol", "SignalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EnsembleSignals_SignalDate",
                table: "EnsembleSignals",
                column: "SignalDate");

            // Indexes for FeatureSnapshots
            migrationBuilder.CreateIndex(
                name: "IX_FeatureSnapshots_TradeId",
                table: "FeatureSnapshots",
                column: "TradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSnapshots_Symbol_CapturedAt",
                table: "FeatureSnapshots",
                columns: new[] { "Symbol", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSnapshots_TradeOutcome",
                table: "FeatureSnapshots",
                column: "TradeOutcome");

            // Indexes for MlModels
            migrationBuilder.CreateIndex(
                name: "IX_MlModels_MarketCode_IsActive",
                table: "MlModels",
                columns: new[] { "MarketCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MlModels_MarketCode_ModelVersion",
                table: "MlModels",
                columns: new[] { "MarketCode", "ModelVersion" },
                unique: true);

            // Indexes for TradeReviews
            migrationBuilder.CreateIndex(
                name: "IX_TradeReviews_TradeId",
                table: "TradeReviews",
                column: "TradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeReviews_Symbol_ReviewedAt",
                table: "TradeReviews",
                columns: new[] { "Symbol", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeReviews_OutcomeClass",
                table: "TradeReviews",
                column: "OutcomeClass");

            migrationBuilder.CreateIndex(
                name: "IX_TradeReviews_MarketCode",
                table: "TradeReviews",
                column: "MarketCode");

            // Indexes for StrategyDecayAlerts
            migrationBuilder.CreateIndex(
                name: "IX_StrategyDecayAlerts_StrategyId",
                table: "StrategyDecayAlerts",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyDecayAlerts_AlertType_IsResolved",
                table: "StrategyDecayAlerts",
                columns: new[] { "AlertType", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyDecayAlerts_MarketCode",
                table: "StrategyDecayAlerts",
                column: "MarketCode");

            // Indexes for MistakePatternReports
            migrationBuilder.CreateIndex(
                name: "IX_MistakePatternReports_MarketCode",
                table: "MistakePatternReports",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_MistakePatternReports_AnalyzedAt",
                table: "MistakePatternReports",
                column: "AnalyzedAt");

            // Indexes for MonthlyAttributions
            migrationBuilder.CreateIndex(
                name: "IX_MonthlyAttributions_MarketCode_Year_Month",
                table: "MonthlyAttributions",
                columns: new[] { "MarketCode", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MonthlyAttributions");
            migrationBuilder.DropTable(name: "MistakePatternReports");
            migrationBuilder.DropTable(name: "StrategyDecayAlerts");
            migrationBuilder.DropTable(name: "TradeReviews");
            migrationBuilder.DropTable(name: "MlModels");
            migrationBuilder.DropTable(name: "FeatureSnapshots");
            migrationBuilder.DropTable(name: "EnsembleSignals");
        }
    }
}
