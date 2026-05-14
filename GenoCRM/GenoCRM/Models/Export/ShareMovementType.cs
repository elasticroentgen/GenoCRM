namespace GenoCRM.Models.Export;

public enum ShareMovementType
{
    Joining,
    Leaving,
    ShareAcquisition,
    ShareCancellation,
    ShareTransfer
}

public enum ShareMovementExportFormat
{
    Csv,
    Xlsx
}

public record ShareMovementExportRequest(
    DateTime From,
    DateTime To,
    IReadOnlySet<ShareMovementType> Types,
    ShareMovementExportFormat Format);
