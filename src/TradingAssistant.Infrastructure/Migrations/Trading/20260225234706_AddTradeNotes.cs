using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Trading
{
    /// <inheritdoc />
    public partial class AddTradeNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeNotes_OrderId",
                table: "TradeNotes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeNotes_PositionId",
                table: "TradeNotes",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeNotes_UserId",
                table: "TradeNotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeNotes");
        }
    }
}
