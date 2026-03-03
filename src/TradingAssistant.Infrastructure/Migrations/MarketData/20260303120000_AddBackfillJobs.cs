using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.MarketData
{
    /// <inheritdoc />
    public partial class AddBackfillJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackfillJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniverseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    YearsBack = table.Column<int>(type: "INTEGER", nullable: false),
                    IsIncremental = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSymbols = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedSymbols = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedSymbols = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorLog = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackfillJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackfillJobs_UniverseId",
                table: "BackfillJobs",
                column: "UniverseId");

            migrationBuilder.CreateIndex(
                name: "IX_BackfillJobs_Status",
                table: "BackfillJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackfillJobs");
        }
    }
}
