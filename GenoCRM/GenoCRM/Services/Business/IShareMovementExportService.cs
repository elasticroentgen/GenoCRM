using GenoCRM.Models.Export;

namespace GenoCRM.Services.Business;

public interface IShareMovementExportService
{
    Task<ExportResult> ExportAsync(ShareMovementExportRequest request);
}
