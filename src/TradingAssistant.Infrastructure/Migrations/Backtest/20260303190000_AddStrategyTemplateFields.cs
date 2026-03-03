using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingAssistant.Infrastructure.Migrations.Backtest
{
    /// <inheritdoc />
    public partial class AddStrategyTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Strategies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateMarketCode",
                table: "Strategies",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                table: "Strategies",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateRegimes",
                table: "Strategies",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_IsTemplate_TemplateMarketCode",
                table: "Strategies",
                columns: new[] { "IsTemplate", "TemplateMarketCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Strategies_IsTemplate_TemplateMarketCode",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "TemplateRegimes",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "TemplateMarketCode",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Strategies");
        }
    }
}
