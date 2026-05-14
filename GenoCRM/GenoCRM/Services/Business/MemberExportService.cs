using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GenoCRM.Models.Domain;
using GenoCRM.Models.Export;
using GenoCRM.Resources.Pages;
using Microsoft.Extensions.Localization;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace GenoCRM.Services.Business;

public class MemberExportService : IMemberExportService
{
    private readonly IStringLocalizer<SharedResource> _shared;
    private readonly IStringLocalizer<Members> _members;

    public MemberExportService(
        IStringLocalizer<SharedResource> shared,
        IStringLocalizer<Members> members)
    {
        _shared = shared;
        _members = members;
    }

    public ExportResult Export(
        IEnumerable<Member> members,
        IReadOnlyCollection<MemberExportField> fields,
        MemberExportFormat format)
    {
        var orderedFields = Enum.GetValues<MemberExportField>()
            .Where(fields.Contains)
            .ToList();
        var memberList = members.ToList();
        var headers = orderedFields.Select(GetHeader).ToList();
        var rows = memberList.Select(m => orderedFields.Select(f => GetValue(m, f)).ToList()).ToList();

        return format switch
        {
            MemberExportFormat.Csv => WriteCsv(headers, rows),
            MemberExportFormat.Xlsx => WriteXlsx(orderedFields, headers, rows, memberList),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private string GetHeader(MemberExportField field)
    {
        var meta = MemberExportFieldMetadata.FieldHeader[field];
        return meta.Source == HeaderSource.Shared
            ? _shared[meta.Key]
            : _members[meta.Key];
    }

    private string GetValue(Member m, MemberExportField field)
    {
        return field switch
        {
            MemberExportField.MemberNumber => m.MemberNumber,
            MemberExportField.MemberType => _shared[m.MemberType.ToString()],
            MemberExportField.Prefix => m.Prefix ?? string.Empty,
            MemberExportField.FirstName => m.FirstName,
            MemberExportField.LastName => m.LastName,
            MemberExportField.CompanyName => m.CompanyName,
            MemberExportField.ContactPerson => m.ContactPerson ?? string.Empty,
            MemberExportField.Email => m.Email,
            MemberExportField.Phone => m.Phone,
            MemberExportField.Street => m.Street,
            MemberExportField.PostalCode => m.PostalCode,
            MemberExportField.City => m.City,
            MemberExportField.Country => m.Country,
            MemberExportField.BirthDate => FormatDate(m.BirthDate),
            MemberExportField.JoinDate => FormatDate(m.JoinDate),
            MemberExportField.LeaveDate => FormatDate(m.LeaveDate),
            MemberExportField.Status => _shared[m.Status.ToString()],
            MemberExportField.Notes => m.Notes ?? string.Empty,
            MemberExportField.TotalShareCount => m.TotalShareCount.ToString(CultureInfo.CurrentCulture),
            MemberExportField.TotalShareValue => m.TotalShareValue.ToString("N2", CultureInfo.CurrentCulture),
            _ => string.Empty
        };
    }

    private static string FormatDate(DateTime? d) =>
        d?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

    private static string FormatDate(DateTime d) =>
        d.ToString("d", CultureInfo.CurrentCulture);

    private ExportResult WriteCsv(List<string> headers, List<List<string>> rows)
    {
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

            foreach (var row in rows)
            {
                foreach (var cell in row) csv.WriteField(cell);
                csv.NextRecord();
            }
        }

        return new ExportResult(
            ms.ToArray(),
            $"genocrm-mitglieder-{DateTime.Now:yyyy-MM-dd}.csv",
            "text/csv");
    }

    private ExportResult WriteXlsx(
        List<MemberExportField> orderedFields,
        List<string> headers,
        List<List<string>> rows,
        List<Member> memberList)
    {
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add(_members["ExportWorksheetName"]);

        for (int c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cells[1, c + 1];
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        for (int r = 0; r < memberList.Count; r++)
        {
            var member = memberList[r];
            for (int c = 0; c < orderedFields.Count; c++)
            {
                var field = orderedFields[c];
                var cell = ws.Cells[r + 2, c + 1];
                WriteXlsxCell(cell, member, field);
            }
        }

        if (ws.Dimension is not null)
        {
            ws.View.FreezePanes(2, 1);
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        return new ExportResult(
            pkg.GetAsByteArray(),
            $"genocrm-mitglieder-{DateTime.Now:yyyy-MM-dd}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private void WriteXlsxCell(ExcelRange cell, Member m, MemberExportField field)
    {
        switch (field)
        {
            case MemberExportField.BirthDate:
                if (m.BirthDate.HasValue) { cell.Value = m.BirthDate.Value; cell.Style.Numberformat.Format = "yyyy-mm-dd"; }
                break;
            case MemberExportField.JoinDate:
                cell.Value = m.JoinDate; cell.Style.Numberformat.Format = "yyyy-mm-dd";
                break;
            case MemberExportField.LeaveDate:
                if (m.LeaveDate.HasValue) { cell.Value = m.LeaveDate.Value; cell.Style.Numberformat.Format = "yyyy-mm-dd"; }
                break;
            case MemberExportField.TotalShareCount:
                cell.Value = m.TotalShareCount;
                break;
            case MemberExportField.TotalShareValue:
                cell.Value = m.TotalShareValue;
                cell.Style.Numberformat.Format = "#,##0.00 €";
                break;
            default:
                cell.Value = GetValue(m, field);
                break;
        }
    }
}
