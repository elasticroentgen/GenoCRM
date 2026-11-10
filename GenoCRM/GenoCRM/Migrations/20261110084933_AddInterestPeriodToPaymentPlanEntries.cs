using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddInterestPeriodToPaymentPlanEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InterestDays",
                table: "LoanPaymentPlanEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterestPeriodEnd",
                table: "LoanPaymentPlanEntries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterestPeriodStart",
                table: "LoanPaymentPlanEntries",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterestDays",
                table: "LoanPaymentPlanEntries");

            migrationBuilder.DropColumn(
                name: "InterestPeriodEnd",
                table: "LoanPaymentPlanEntries");

            migrationBuilder.DropColumn(
                name: "InterestPeriodStart",
                table: "LoanPaymentPlanEntries");
        }
    }
}
