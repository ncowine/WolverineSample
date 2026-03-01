using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Trading
{
    /// <inheritdoc />
    public partial class AddDcaExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DcaExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DcaPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ExecutedPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DcaExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DcaExecutions_DcaPlans_DcaPlanId",
                        column: x => x.DcaPlanId,
                        principalTable: "DcaPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DcaExecutions_DcaPlanId",
                table: "DcaExecutions",
                column: "DcaPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DcaExecutions");
        }
    }
}
