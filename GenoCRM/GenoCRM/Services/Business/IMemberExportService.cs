using GenoCRM.Models.Domain;
using GenoCRM.Models.Export;

namespace GenoCRM.Services.Business;

public interface IMemberExportService
{
    ExportResult Export(
        IEnumerable<Member> members,
        IReadOnlyCollection<MemberExportField> fields,
        MemberExportFormat format);
}

public record ExportResult(byte[] Bytes, string FileName, string ContentType);
