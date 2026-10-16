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

            // Existing databases may hold duplicate member-level dividends from running the
            // calculation twice (the bug this index guards against). Per member and fiscal year
            // keep one row (a paid one if any, else the oldest) and delete the other UNPAID rows.
            // Paid rows are never deleted; if two paid duplicates exist the index creation fails
            // on purpose so a person resolves them.
            migrationBuilder.Sql(@"
                DELETE FROM ""Dividends"" d
                WHERE d.""ShareId"" IS NULL
                  AND d.""Status"" <> 2
                  AND d.""Id"" <> (
                      SELECT x.""Id"" FROM ""Dividends"" x
                      WHERE x.""MemberId"" = d.""MemberId""
                        AND x.""FiscalYear"" = d.""FiscalYear""
                        AND x.""ShareId"" IS NULL
                      ORDER BY (x.""Status"" = 2) DESC, x.""Id"" ASC
                      LIMIT 1);
            ");

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
