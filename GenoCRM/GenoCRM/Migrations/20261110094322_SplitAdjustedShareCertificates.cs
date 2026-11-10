using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenoCRM.Migrations
{
    /// <summary>
    /// Data fix: some subsequent share subscriptions ("Nachzeichnungen") were booked by raising the
    /// quantity of an existing certificate via "Anteile anpassen" instead of creating a new
    /// certificate via "Anteil hinzufügen". That hides the acquisition from the §30 GenG movement
    /// export for the year the original share was paid. This migration moves each subsequent
    /// subscription into its own certificate together with its payment.
    ///
    /// Every block is guarded (certificate number, current quantity, payment id/amount/date still
    /// attached), so it is a no-op on databases that do not contain exactly this data and when
    /// it runs again.
    /// </summary>
    public partial class SplitAdjustedShareCertificates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CERT012 (M012): 1 share paid 2025-10-09, 3 more subscribed and paid 2026-05-17
            migrationBuilder.Sql(SplitSql("CERT012", currentQuantity: 4, movedQuantity: 3,
                paymentId: 61, amount: 750.00m, paymentDate: "2026-05-17"));

            // CERT038 (M038): 1 share paid 2025-11-18, 3 more subscribed and paid 2026-05-26
            migrationBuilder.Sql(SplitSql("CERT038", currentQuantity: 4, movedQuantity: 3,
                paymentId: 65, amount: 750.00m, paymentDate: "2026-05-26"));

            // CERT015 (M015): 4 shares paid 2025-09-18, raised to 6 with payment 2025-12-23
            migrationBuilder.Sql(SplitSql("CERT015", currentQuantity: 6, movedQuantity: 2,
                paymentId: 47, amount: 500.00m, paymentDate: "2025-12-23"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data fix only; not reversible automatically.
        }

        private static string SplitSql(string certificate, int currentQuantity, int movedQuantity,
            int paymentId, decimal amount, string paymentDate)
        {
            var amountSql = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $$"""
                DO $$
                DECLARE
                    old_share "CooperativeShares"%ROWTYPE;
                    new_cert  text;
                    new_id    integer;
                BEGIN
                    SELECT s.* INTO old_share
                    FROM "CooperativeShares" s
                    WHERE s."CertificateNumber" = '{{certificate}}'
                      AND s."Quantity" = {{currentQuantity}}
                      AND s."Status" = 'Active'
                      AND EXISTS (
                          SELECT 1 FROM "Payments" p
                          WHERE p."Id" = {{paymentId}}
                            AND p."ShareId" = s."Id"
                            AND p."Amount" = {{amountSql}}
                            AND p."PaymentDate"::date = DATE '{{paymentDate}}');

                    IF NOT FOUND THEN
                        RAISE NOTICE 'SplitAdjustedShareCertificates: {{certificate}} not in expected state, skipped';
                        RETURN;
                    END IF;

                    SELECT 'CERT' || lpad((COALESCE(MAX(substring("CertificateNumber" FROM 5)::int), 0) + 1)::text, 3, '0')
                    INTO new_cert
                    FROM "CooperativeShares"
                    WHERE "CertificateNumber" ~ '^CERT[0-9]{3,}$';

                    INSERT INTO "CooperativeShares"
                        ("MemberId", "CertificateNumber", "Quantity", "NominalValue", "Value",
                         "IssueDate", "Status", "Notes", "CreatedAt", "UpdatedAt")
                    VALUES
                        (old_share."MemberId", new_cert, {{movedQuantity}}, old_share."NominalValue", old_share."Value",
                         TIMESTAMP '{{paymentDate}}', 'Active',
                         'Ausgegliedert aus {{certificate}} (Nachzeichnung vom {{paymentDate}}, ursprünglich über "Anteile anpassen" erfasst)',
                         now() AT TIME ZONE 'UTC', now() AT TIME ZONE 'UTC')
                    RETURNING "Id" INTO new_id;

                    UPDATE "Payments"
                    SET "ShareId" = new_id, "UpdatedAt" = now() AT TIME ZONE 'UTC'
                    WHERE "Id" = {{paymentId}};

                    UPDATE "CooperativeShares"
                    SET "Quantity" = "Quantity" - {{movedQuantity}},
                        "Notes" = COALESCE("Notes" || E'\n\n', '') || '[' || to_char(now(), 'YYYY-MM-DD') || '] Nachzeichnung ({{movedQuantity}} Anteile) in ' || new_cert || ' ausgegliedert',
                        "UpdatedAt" = now() AT TIME ZONE 'UTC'
                    WHERE "Id" = old_share."Id";
                END $$;
                """;
        }
    }
}
