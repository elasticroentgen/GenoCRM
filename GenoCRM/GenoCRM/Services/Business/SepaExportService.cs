using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

/// <summary>Account the SEPA credit transfers are paid from (the cooperative's account).</summary>
public class SepaDebtorSettings
{
    public string Name { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string? Bic { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Iban);
}

public record SepaExportOptions(
    /// <summary>Fixed execution date for all payments; null = due date (or today if the due date is in the past).</summary>
    DateTime? ExecutionDate = null);

public record SepaExportResult(
    byte[] Content,
    string FileName,
    string MessageId,
    int TransactionCount,
    decimal ControlSum,
    IReadOnlyList<LoanPaymentPlanEntry> Exported,
    IReadOnlyList<LoanPaymentPlanEntry> Skipped);

public interface ISepaExportService
{
    SepaDebtorSettings Debtor { get; }

    /// <summary>Builds a pain.001.001.09 credit transfer file for the given entries and marks them as exported.</summary>
    Task<SepaExportResult> ExportLoanPaymentsAsync(IReadOnlyCollection<int> entryIds, SepaExportOptions options);

    /// <summary>
    /// Rebuilds the file of an earlier export (same message id) without changing anything in the database:
    /// entries are not marked again and no notifications are triggered.
    /// </summary>
    Task<SepaExportResult> RebuildExportAsync(string exportReference, SepaExportOptions options);
}

public class SepaExportService : ISepaExportService
{
    public const string Pain001Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.09";

    private readonly GenoDbContext _context;
    private readonly ILogger<SepaExportService> _logger;

    public SepaDebtorSettings Debtor { get; }

    public SepaExportService(GenoDbContext context, IConfiguration configuration, ILogger<SepaExportService> logger)
    {
        _context = context;
        _logger = logger;
        Debtor = configuration.GetSection("SepaDebtor").Get<SepaDebtorSettings>() ?? new SepaDebtorSettings();
    }

    public async Task<SepaExportResult> ExportLoanPaymentsAsync(IReadOnlyCollection<int> entryIds, SepaExportOptions options)
    {
        if (!Debtor.IsConfigured)
            throw new InvalidOperationException("SEPA debtor account is not configured (SepaDebtor:Name / SepaDebtor:Iban).");

        var entries = await EntriesWithDetails()
            .Where(e => entryIds.Contains(e.Id) && e.Status != PaymentPlanEntryStatus.Paid)
            .ToListAsync();

        var exportable = entries.Where(e => IsExportable(e)).ToList();
        var skipped = entries.Except(exportable).ToList();
        if (exportable.Count == 0)
            throw new InvalidOperationException("No exportable payments (missing IBAN or zero amount).");

        var now = DateTime.Now;
        var messageId = $"GENO-{now:yyyyMMddHHmmss}";
        var content = BuildPain001(exportable, Debtor, messageId, now, options.ExecutionDate, DateTime.Today);

        var exportedAt = DateTime.UtcNow;
        foreach (var entry in exportable)
        {
            entry.ExportedAt = exportedAt;
            entry.ExportReference = messageId;
            entry.UpdatedAt = exportedAt;
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("SEPA export {MessageId} with {Count} payments ({Sum} EUR), {Skipped} skipped",
            messageId, exportable.Count, exportable.Sum(e => e.TotalAmount), skipped.Count);

        return new SepaExportResult(content, $"SEPA-Ueberweisungen-{now:yyyy-MM-dd-HHmm}.xml", messageId,
            exportable.Count, exportable.Sum(e => e.TotalAmount), exportable, skipped);
    }

    public async Task<SepaExportResult> RebuildExportAsync(string exportReference, SepaExportOptions options)
    {
        if (!Debtor.IsConfigured)
            throw new InvalidOperationException("SEPA debtor account is not configured (SepaDebtor:Name / SepaDebtor:Iban).");

        // Paid entries are included on purpose: the file is reproduced as it was exported.
        var entries = await EntriesWithDetails()
            .AsNoTracking()
            .Where(e => e.ExportReference == exportReference)
            .ToListAsync();
        var exportable = entries.Where(IsExportable).ToList();
        var skipped = entries.Except(exportable).ToList();
        if (exportable.Count == 0)
            throw new InvalidOperationException($"No exportable payments found for export {exportReference}.");

        var content = BuildPain001(exportable, Debtor, exportReference, DateTime.Now, options.ExecutionDate, DateTime.Today);

        _logger.LogInformation("SEPA export {MessageId} rebuilt (re-download) with {Count} payments; no entries changed",
            exportReference, exportable.Count);

        return new SepaExportResult(content, $"SEPA-Ueberweisungen-{exportReference}-Kopie.xml", exportReference,
            exportable.Count, exportable.Sum(e => e.TotalAmount), exportable, skipped);
    }

    private IQueryable<LoanPaymentPlanEntry> EntriesWithDetails() =>
        _context.LoanPaymentPlanEntries
            .Include(e => e.LoanPaymentPlan)
                .ThenInclude(p => p.LoanSubscription)
                    .ThenInclude(s => s.Member)
            .Include(e => e.LoanPaymentPlan)
                .ThenInclude(p => p.LoanSubscription)
                    .ThenInclude(s => s.LoanOffer)
                        .ThenInclude(o => o.LoanProject);

    /// <summary>Requested execution date: the fixed date if given, else the due date (today if already due).</summary>
    public static DateTime GetExecutionDate(LoanPaymentPlanEntry entry, DateTime? fixedExecutionDate, DateTime today) =>
        (fixedExecutionDate ?? (entry.DueDate.Date < today ? today : entry.DueDate.Date)).Date;

    public static bool IsExportable(LoanPaymentPlanEntry entry) =>
        entry.TotalAmount > 0 && !string.IsNullOrWhiteSpace(entry.LoanPaymentPlan.LoanSubscription.IBAN);

    /// <summary>Remittance text shown on the creditor's bank statement, e.g. "Zinsen 2026 LS0008 - Stausee Oberwald".</summary>
    public static string GetRemittanceInformation(LoanPaymentPlanEntry entry)
    {
        var sub = entry.LoanPaymentPlan.LoanSubscription;
        var project = sub.LoanOffer.LoanProject.Title;
        if (entry.PrincipalAmount > 0 && entry.InterestAmount > 0)
            return $"Tilgung + Zinsen {sub.SubscriptionNumber} - {project}";
        if (entry.PrincipalAmount > 0)
            return $"Tilgung {sub.SubscriptionNumber} - {project}";
        var year = (entry.InterestPeriodStart ?? entry.DueDate).Year;
        return $"Zinsen {year} {sub.SubscriptionNumber} - {project}";
    }

    /// <summary>
    /// Builds a SEPA credit transfer initiation (pain.001.001.09). Payments are grouped into one
    /// PmtInf block per execution date; each payment is booked individually (BtchBookg = false).
    /// </summary>
    public static byte[] BuildPain001(
        IReadOnlyList<LoanPaymentPlanEntry> entries,
        SepaDebtorSettings debtor,
        string messageId,
        DateTime createdAt,
        DateTime? fixedExecutionDate,
        DateTime today)
    {
        var groups = entries
            .GroupBy(e => GetExecutionDate(e, fixedExecutionDate, today))
            .OrderBy(g => g.Key)
            .ToList();

        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var stream = new MemoryStream();
        using (var w = XmlWriter.Create(stream, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("Document", Pain001Namespace);
            w.WriteStartElement("CstmrCdtTrfInitn");

            w.WriteStartElement("GrpHdr");
            w.WriteElementString("MsgId", messageId);
            w.WriteElementString("CreDtTm", createdAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            w.WriteElementString("NbOfTxs", entries.Count.ToString(CultureInfo.InvariantCulture));
            w.WriteElementString("CtrlSum", FormatAmount(entries.Sum(e => e.TotalAmount)));
            w.WriteStartElement("InitgPty");
            w.WriteElementString("Nm", Sanitize(debtor.Name, 70));
            w.WriteEndElement();
            w.WriteEndElement(); // GrpHdr

            var block = 0;
            foreach (var group in groups)
            {
                block++;
                w.WriteStartElement("PmtInf");
                w.WriteElementString("PmtInfId", $"{messageId}-{block}");
                w.WriteElementString("PmtMtd", "TRF");
                w.WriteElementString("BtchBookg", "false");
                w.WriteElementString("NbOfTxs", group.Count().ToString(CultureInfo.InvariantCulture));
                w.WriteElementString("CtrlSum", FormatAmount(group.Sum(e => e.TotalAmount)));
                w.WriteStartElement("PmtTpInf");
                w.WriteStartElement("SvcLvl");
                w.WriteElementString("Cd", "SEPA");
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteStartElement("ReqdExctnDt");
                w.WriteElementString("Dt", group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                w.WriteEndElement();
                w.WriteStartElement("Dbtr");
                w.WriteElementString("Nm", Sanitize(debtor.Name, 70));
                w.WriteEndElement();
                w.WriteStartElement("DbtrAcct");
                w.WriteStartElement("Id");
                w.WriteElementString("IBAN", NormalizeIban(debtor.Iban));
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteStartElement("DbtrAgt");
                WriteFinInstnId(w, debtor.Bic, required: true);
                w.WriteEndElement();
                w.WriteElementString("ChrgBr", "SLEV");

                foreach (var entry in group.OrderBy(e => e.LoanPaymentPlan.LoanSubscription.Member.LastName)
                             .ThenBy(e => e.LoanPaymentPlan.LoanSubscription.SubscriptionNumber))
                {
                    var sub = entry.LoanPaymentPlan.LoanSubscription;
                    w.WriteStartElement("CdtTrfTxInf");
                    w.WriteStartElement("PmtId");
                    w.WriteElementString("EndToEndId", $"GENO-{entry.Id}");
                    w.WriteEndElement();
                    w.WriteStartElement("Amt");
                    w.WriteStartElement("InstdAmt");
                    w.WriteAttributeString("Ccy", "EUR");
                    w.WriteString(FormatAmount(entry.TotalAmount));
                    w.WriteEndElement();
                    w.WriteEndElement();
                    if (!string.IsNullOrWhiteSpace(sub.BIC))
                    {
                        w.WriteStartElement("CdtrAgt");
                        WriteFinInstnId(w, sub.BIC, required: false);
                        w.WriteEndElement();
                    }
                    w.WriteStartElement("Cdtr");
                    var creditorName = string.IsNullOrWhiteSpace(sub.BankAccountHolder) ? sub.Member.FullName : sub.BankAccountHolder;
                    w.WriteElementString("Nm", Sanitize(creditorName, 70));
                    w.WriteEndElement();
                    w.WriteStartElement("CdtrAcct");
                    w.WriteStartElement("Id");
                    w.WriteElementString("IBAN", NormalizeIban(sub.IBAN));
                    w.WriteEndElement();
                    w.WriteEndElement();
                    w.WriteStartElement("RmtInf");
                    w.WriteElementString("Ustrd", Sanitize(GetRemittanceInformation(entry), 140));
                    w.WriteEndElement();
                    w.WriteEndElement(); // CdtTrfTxInf
                }

                w.WriteEndElement(); // PmtInf
            }

            w.WriteEndElement(); // CstmrCdtTrfInitn
            w.WriteEndElement(); // Document
            w.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void WriteFinInstnId(XmlWriter w, string? bic, bool required)
    {
        w.WriteStartElement("FinInstnId");
        if (!string.IsNullOrWhiteSpace(bic))
        {
            w.WriteElementString("BICFI", bic.Replace(" ", "").ToUpperInvariant());
        }
        else if (required)
        {
            w.WriteStartElement("Othr");
            w.WriteElementString("Id", "NOTPROVIDED");
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static string FormatAmount(decimal amount) =>
        Math.Round(amount, 2).ToString("0.00", CultureInfo.InvariantCulture);

    public static string NormalizeIban(string iban) => iban.Replace(" ", "").ToUpperInvariant();

    /// <summary>
    /// Restricts text to the SEPA basic Latin character set (German umlauts are transliterated)
    /// and truncates it to <paramref name="maxLength"/>.
    /// </summary>
    public static string Sanitize(string value, int maxLength)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            switch (c)
            {
                case 'ä': sb.Append("ae"); break;
                case 'ö': sb.Append("oe"); break;
                case 'ü': sb.Append("ue"); break;
                case 'Ä': sb.Append("Ae"); break;
                case 'Ö': sb.Append("Oe"); break;
                case 'Ü': sb.Append("Ue"); break;
                case 'ß': sb.Append("ss"); break;
                case '&': sb.Append('+'); break;
                default:
                    if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' || "/-?:().,'+ ".Contains(c))
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        // Strip diacritics (é -> e), drop anything else
                        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
                        var baseChar = decomposed[0];
                        if (baseChar is >= 'a' and <= 'z' or >= 'A' and <= 'Z') sb.Append(baseChar);
                        else sb.Append(' ');
                    }
                    break;
            }
        }

        var result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " {2,}", " ").Trim();
        return result.Length > maxLength ? result[..maxLength].TrimEnd() : result;
    }
}
