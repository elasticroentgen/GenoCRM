using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddExportInfoToPaymentPlanEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExportReference",
                table: "LoanPaymentPlanEntries",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExportedAt",
                table: "LoanPaymentPlanEntries",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportReference",
                table: "LoanPaymentPlanEntries");

            migrationBuilder.DropColumn(
                name: "ExportedAt",
                table: "LoanPaymentPlanEntries");
        }
    }
}
