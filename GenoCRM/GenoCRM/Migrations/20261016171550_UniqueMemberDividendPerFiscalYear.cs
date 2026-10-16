using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class UniqueMemberDividendPerFiscalYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dividends_MemberId_FiscalYear",
                table: "Dividends");

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_MemberId_FiscalYear",
                table: "Dividends",
                columns: new[] { "MemberId", "FiscalYear" },
                unique: true,
                filter: "\"ShareId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dividends_MemberId_FiscalYear",
                table: "Dividends");

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_MemberId_FiscalYear",
                table: "Dividends",
                columns: new[] { "MemberId", "FiscalYear" });
        }
    }
}
