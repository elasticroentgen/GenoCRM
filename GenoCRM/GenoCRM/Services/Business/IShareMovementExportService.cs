using GenoCRM.Models.Export;

namespace GenoCRM.Services.Business;

public interface IShareMovementExportService
{
    Task<ExportResult> ExportAsync(ShareMovementExportRequest request);

    /// <summary>
    /// Returns the rows that <see cref="ExportAsync"/> would write, already formatted for display.
    /// </summary>
    Task<ShareMovementPreview> PreviewAsync(ShareMovementExportRequest request);
}

public record ShareMovementPreview(
    string[] Headers,
    string[][] Rows,
    IReadOnlyDictionary<ShareMovementType, int> CountsByType);
