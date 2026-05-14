using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Models.Export;
using GenoCRM.Resources.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace GenoCRM.Services.Business;

public class ShareMovementExportService : IShareMovementExportService
{
    private readonly GenoDbContext _db;
    private readonly IStringLocalizer<SharedResource> _shared;
    private readonly IStringLocalizer<Shares> _shares;

    public ShareMovementExportService(
        GenoDbContext db,
        IStringLocalizer<SharedResource> shared,
        IStringLocalizer<Shares> shares)
    {
        _db = db;
        _shared = shared;
        _shares = shares;
    }

    public async Task<ExportResult> ExportAsync(ShareMovementExportRequest request)
    {
        var fromUtc = DateTime.SpecifyKind(request.From.Date, DateTimeKind.Utc);
        var toExclusiveUtc = DateTime.SpecifyKind(request.To.Date.AddDays(1), DateTimeKind.Utc);

        var rows = new List<ShareMovementRow>();

        if (request.Types.Contains(ShareMovementType.Joining))
        {
            var joiners = await _db.Members
                .IgnoreQueryFilters()
                .Where(m => m.JoinDate >= fromUtc && m.JoinDate < toExclusiveUtc)
                .ToListAsync();

            rows.AddRange(joiners.Select(m => BuildMemberRow(m, ShareMovementType.Joining, m.JoinDate, m.Notes)));
        }

        if (request.Types.Contains(ShareMovementType.Leaving))
        {
            var leavers = await _db.Members
                .IgnoreQueryFilters()
                .Where(m => m.LeaveDate != null
                            && m.LeaveDate >= fromUtc
                            && m.LeaveDate < toExclusiveUtc)
                .ToListAsync();

            rows.AddRange(leavers.Select(m => BuildMemberRow(m, ShareMovementType.Leaving, m.LeaveDate!.Value, m.Notes)));
        }

        if (request.Types.Contains(ShareMovementType.ShareAcquisition))
        {
            var acquisitions = await _db.CooperativeShares
                .IgnoreQueryFilters()
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .Where(s => s.IssueDate >= fromUtc && s.IssueDate < toExclusiveUtc)
                .ToListAsync();

            rows.AddRange(acquisitions
                .Where(s => s.IsFullyPaid)
                .Select(s => BuildShareRow(s, ShareMovementType.ShareAcquisition, s.IssueDate)));
        }

        if (request.Types.Contains(ShareMovementType.ShareCancellation))
        {
            var cancellations = await _db.CooperativeShares
                .IgnoreQueryFilters()
                .Include(s => s.Member)
                .Include(s => s.Payments)
                .Where(s => s.CancellationDate != null
                            && s.CancellationDate >= fromUtc
                            && s.CancellationDate < toExclusiveUtc
                            && s.Status == ShareStatus.Cancelled)
                .ToListAsync();

            rows.AddRange(cancellations
                .Where(s => s.IsFullyPaid)
                .Select(s => BuildShareRow(s, ShareMovementType.ShareCancellation, s.CancellationDate!.Value)));
        }

        if (request.Types.Contains(ShareMovementType.ShareTransfer))
        {
            var transfers = await _db.ShareTransfers
                .IgnoreQueryFilters()
                .Include(t => t.FromMember)
                .Include(t => t.ToMember)
                .Include(t => t.Share).ThenInclude(s => s.Payments)
                .Where(t => t.Status == ShareTransferStatus.Completed
                            && t.CompletionDate != null
                            && t.CompletionDate >= fromUtc
                            && t.CompletionDate < toExclusiveUtc)
                .ToListAsync();

            rows.AddRange(transfers
                .Where(t => t.Share.IsFullyPaid)
                .Select(BuildTransferRow));
        }

        var ordered = rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.MemberNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return request.Format switch
        {
            ShareMovementExportFormat.Csv => WriteCsv(ordered),
            ShareMovementExportFormat.Xlsx => WriteXlsx(ordered),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Format))
        };
    }

    private static ShareMovementRow BuildMemberRow(Member m, ShareMovementType type, DateTime date, string? notes) =>
        new()
        {
            Date = date,
            Type = type,
            MemberNumber = m.MemberNumber,
            LastName = m.MemberType == MemberType.Individual ? m.LastName : string.Empty,
            FirstName = m.MemberType == MemberType.Individual ? m.FirstName : string.Empty,
            CompanyName = m.MemberType == MemberType.Company ? m.CompanyName : string.Empty,
            Street = m.Street,
            PostalCode = m.PostalCode,
            City = m.City,
            Country = m.Country,
            Notes = notes
        };

    private static ShareMovementRow BuildShareRow(CooperativeShare s, ShareMovementType type, DateTime date)
    {
        var row = BuildMemberRow(s.Member, type, date, s.Notes);
        return new ShareMovementRow
        {
            Date = row.Date,
            Type = row.Type,
            MemberNumber = row.MemberNumber,
            LastName = row.LastName,
            FirstName = row.FirstName,
            CompanyName = row.CompanyName,
            Street = row.Street,
            PostalCode = row.PostalCode,
            City = row.City,
            Country = row.Country,
            Quantity = s.Quantity,
            NominalValue = s.NominalValue,
            TotalValue = s.TotalValue,
            CertificateNumber = s.CertificateNumber,
            Notes = row.Notes
        };
    }

    private static ShareMovementRow BuildTransferRow(ShareTransfer t)
    {
        var row = BuildMemberRow(t.FromMember, ShareMovementType.ShareTransfer, t.CompletionDate!.Value, t.Notes);
        return new ShareMovementRow
        {
            Date = row.Date,
            Type = row.Type,
            MemberNumber = row.MemberNumber,
            LastName = row.LastName,
            FirstName = row.FirstName,
            CompanyName = row.CompanyName,
            Street = row.Street,
            PostalCode = row.PostalCode,
            City = row.City,
            Country = row.Country,
            Quantity = t.Quantity,
            NominalValue = t.Share.NominalValue,
            TotalValue = t.TotalValue,
            CertificateNumber = t.Share.CertificateNumber,
            CounterMemberNumber = t.ToMember.MemberNumber,
            CounterMemberName = t.ToMember.FullName,
            Notes = row.Notes
        };
    }

    private (string[] Headers, string[][] Rows) BuildTable(IReadOnlyList<ShareMovementRow> rows)
    {
        var headers = new string[]
        {
            _shares["ExportField_Date"].Value,
            _shares["ExportField_MovementType"].Value,
            _shared["MemberNumber"].Value,
            _shared["LastName"].Value,
            _shared["FirstName"].Value,
            _shared["CompanyName"].Value,
            _shared["Street"].Value,
            _shared["PostalCode"].Value,
            _shared["City"].Value,
            _shared["Country"].Value,
            _shared["Quantity"].Value,
            _shared["NominalValue"].Value,
            _shared["TotalValue"].Value,
            _shared["CertificateNumber"].Value,
            _shares["ExportField_CounterMemberNumber"].Value,
            _shares["ExportField_CounterMemberName"].Value,
            _shared["Notes"].Value
        };

        var data = rows.Select(r => new[]
        {
            FormatDate(r.Date),
            GetMovementTypeLabel(r.Type),
            r.MemberNumber,
            r.LastName,
            r.FirstName,
            r.CompanyName,
            r.Street,
            r.PostalCode,
            r.City,
            r.Country,
            r.Quantity?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            r.NominalValue?.ToString("N2", CultureInfo.CurrentCulture) ?? string.Empty,
            r.TotalValue?.ToString("N2", CultureInfo.CurrentCulture) ?? string.Empty,
            r.CertificateNumber ?? string.Empty,
            r.CounterMemberNumber ?? string.Empty,
            r.CounterMemberName ?? string.Empty,
            r.Notes ?? string.Empty
        }).ToArray();

        return (headers, data);
    }

    private string GetMovementTypeLabel(ShareMovementType type) =>
        _shares[$"ShareMovement_{type}"];

    private static string FormatDate(DateTime d) =>
        d.ToString("d", CultureInfo.CurrentCulture);

    private ExportResult WriteCsv(IReadOnlyList<ShareMovementRow> rows)
    {
        var (headers, data) = BuildTable(rows);

        var cfg = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = ";",
            ShouldQuote = a =>
                a.Field is not null &&
                a.Field.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
        };

        using var ms = new MemoryStream();
        using (var sw = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        using (var csv = new CsvWriter(sw, cfg))
        {
            foreach (var h in headers) csv.WriteField(h);
            csv.NextRecord();

            foreach (var row in data)
            {
                foreach (var cell in row) csv.WriteField(cell);
                csv.NextRecord();
            }
        }

        return new ExportResult(
            ms.ToArray(),
            $"genocrm-anteilsbewegungen-{DateTime.Now:yyyy-MM-dd}.csv",
            "text/csv");
    }

    private ExportResult WriteXlsx(IReadOnlyList<ShareMovementRow> rows)
    {
        var (headers, _) = BuildTable(rows);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add(_shares["ExportWorksheetName"]);

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cells[1, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = r + 2;

            ws.Cells[line, 1].Value = row.Date;
            ws.Cells[line, 1].Style.Numberformat.Format = "yyyy-mm-dd";
            ws.Cells[line, 2].Value = GetMovementTypeLabel(row.Type);
            ws.Cells[line, 3].Value = row.MemberNumber;
            ws.Cells[line, 4].Value = row.LastName;
            ws.Cells[line, 5].Value = row.FirstName;
            ws.Cells[line, 6].Value = row.CompanyName;
            ws.Cells[line, 7].Value = row.Street;
            ws.Cells[line, 8].Value = row.PostalCode;
            ws.Cells[line, 9].Value = row.City;
            ws.Cells[line, 10].Value = row.Country;

            if (row.Quantity.HasValue)
                ws.Cells[line, 11].Value = row.Quantity.Value;
            if (row.NominalValue.HasValue)
            {
                ws.Cells[line, 12].Value = row.NominalValue.Value;
                ws.Cells[line, 12].Style.Numberformat.Format = "#,##0.00 €";
            }
            if (row.TotalValue.HasValue)
            {
                ws.Cells[line, 13].Value = row.TotalValue.Value;
                ws.Cells[line, 13].Style.Numberformat.Format = "#,##0.00 €";
            }

            ws.Cells[line, 14].Value = row.CertificateNumber ?? string.Empty;
            ws.Cells[line, 15].Value = row.CounterMemberNumber ?? string.Empty;
            ws.Cells[line, 16].Value = row.CounterMemberName ?? string.Empty;
            ws.Cells[line, 17].Value = row.Notes ?? string.Empty;
        }

        if (ws.Dimension is not null)
        {
            ws.View.FreezePanes(2, 1);
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        return new ExportResult(
            pkg.GetAsByteArray(),
            $"genocrm-anteilsbewegungen-{DateTime.Now:yyyy-MM-dd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
