using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddBankInfoToLoanSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LoanSubscriptions' AND column_name = 'BankAccountHolder') THEN
                        ALTER TABLE "LoanSubscriptions" ADD COLUMN "BankAccountHolder" character varying(200) NOT NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LoanSubscriptions' AND column_name = 'IBAN') THEN
                        ALTER TABLE "LoanSubscriptions" ADD COLUMN "IBAN" character varying(34) NOT NULL DEFAULT '';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LoanSubscriptions' AND column_name = 'BIC') THEN
                        ALTER TABLE "LoanSubscriptions" ADD COLUMN "BIC" character varying(11) NOT NULL DEFAULT '';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BankAccountHolder", table: "LoanSubscriptions");
            migrationBuilder.DropColumn(name: "IBAN", table: "LoanSubscriptions");
            migrationBuilder.DropColumn(name: "BIC", table: "LoanSubscriptions");
        }
    }
}
