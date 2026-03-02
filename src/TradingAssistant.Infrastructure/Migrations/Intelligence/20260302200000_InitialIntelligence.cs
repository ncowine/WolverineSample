using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class InitialIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BreadthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdvanceDeclineRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    PctAbove200Sma = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    PctAbove50Sma = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    NewHighs = table.Column<int>(type: "INTEGER", nullable: false),
                    NewLows = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalStocks = table.Column<int>(type: "INTEGER", nullable: false),
                    Advancing = table.Column<int>(type: "INTEGER", nullable: false),
                    Declining = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreadthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrelationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LookbackDays = table.Column<int>(type: "INTEGER", nullable: false),
                    MatrixJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrelationSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CommissionPerShare = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    ExchangeFeePercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    SpreadEstimatePercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VixSymbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DataSource = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    DnaProfileJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    DnaProfileUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketRegimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CurrentRegime = table.Column<int>(type: "INTEGER", nullable: false),
                    RegimeStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RegimeDuration = table.Column<int>(type: "INTEGER", nullable: false),
                    SmaSlope50 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    SmaSlope200 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    VixLevel = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    BreadthScore = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    PctAbove200Sma = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    AdvanceDeclineRatio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRegimes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RunDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegimeTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FromRegime = table.Column<int>(type: "INTEGER", nullable: false),
                    ToRegime = table.Column<int>(type: "INTEGER", nullable: false),
                    TransitionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SmaSlope50 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    SmaSlope200 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    VixLevel = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    BreadthScore = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    PctAbove200Sma = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegimeTransitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreadthSnapshots_MarketCode_SnapshotDate",
                table: "BreadthSnapshots",
                columns: new[] { "MarketCode", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrelationSnapshots_SnapshotDate",
                table: "CorrelationSnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_CostProfiles_MarketCode_IsActive",
                table: "CostProfiles",
                columns: new[] { "MarketCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketProfiles_MarketCode",
                table: "MarketProfiles",
                column: "MarketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketRegimes_ClassifiedAt",
                table: "MarketRegimes",
                column: "ClassifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarketRegimes_MarketCode",
                table: "MarketRegimes",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunLogs_MarketCode_RunDate",
                table: "PipelineRunLogs",
                columns: new[] { "MarketCode", "RunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RegimeTransitions_MarketCode",
                table: "RegimeTransitions",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_RegimeTransitions_TransitionDate",
                table: "RegimeTransitions",
                column: "TransitionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreadthSnapshots");

            migrationBuilder.DropTable(
                name: "CorrelationSnapshots");

            migrationBuilder.DropTable(
                name: "CostProfiles");

            migrationBuilder.DropTable(
                name: "MarketProfiles");

            migrationBuilder.DropTable(
                name: "MarketRegimes");

            migrationBuilder.DropTable(
                name: "PipelineRunLogs");

            migrationBuilder.DropTable(
                name: "RegimeTransitions");
        }
    }
}
