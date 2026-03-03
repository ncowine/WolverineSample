using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Persistence.Migrations.Intelligence
{
    /// <inheritdoc />
    public partial class AddTournamentRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarketCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEntries = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EntryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRuns", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_TournamentEntries_TournamentRunId",
                table: "TournamentEntries",
                column: "TournamentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRuns_MarketCode_Status",
                table: "TournamentRuns",
                columns: new[] { "MarketCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentRuns");

            migrationBuilder.DropIndex(
                name: "IX_TournamentEntries_TournamentRunId",
                table: "TournamentEntries");

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
        }
    }
}
