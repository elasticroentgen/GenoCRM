namespace GenoCRM.Models.Export;

public enum MemberExportFormat
{
    Csv,
    Xlsx
}

public enum MemberExportFieldGroup
{
    Basic,
    Contact,
    Address,
    Membership,
    Shares
}

public enum MemberExportField
{
    MemberNumber,
    MemberType,
    Prefix,
    FirstName,
    LastName,
    CompanyName,
    ContactPerson,
    Email,
    Phone,
    Street,
    PostalCode,
    City,
    Country,
    BirthDate,
    JoinDate,
    LeaveDate,
    Status,
    Notes,
    TotalShareCount,
    TotalShareValue
}

public record MemberExportSelection(HashSet<MemberExportField> Fields, MemberExportFormat Format);

public static class MemberExportFieldMetadata
{
    public static readonly IReadOnlyDictionary<MemberExportField, MemberExportFieldGroup> FieldGroup =
        new Dictionary<MemberExportField, MemberExportFieldGroup>
        {
            [MemberExportField.MemberNumber] = MemberExportFieldGroup.Basic,
            [MemberExportField.MemberType] = MemberExportFieldGroup.Basic,
            [MemberExportField.Prefix] = MemberExportFieldGroup.Basic,
            [MemberExportField.FirstName] = MemberExportFieldGroup.Basic,
            [MemberExportField.LastName] = MemberExportFieldGroup.Basic,
            [MemberExportField.CompanyName] = MemberExportFieldGroup.Basic,
            [MemberExportField.ContactPerson] = MemberExportFieldGroup.Basic,

            [MemberExportField.Email] = MemberExportFieldGroup.Contact,
            [MemberExportField.Phone] = MemberExportFieldGroup.Contact,

            [MemberExportField.Street] = MemberExportFieldGroup.Address,
            [MemberExportField.PostalCode] = MemberExportFieldGroup.Address,
            [MemberExportField.City] = MemberExportFieldGroup.Address,
            [MemberExportField.Country] = MemberExportFieldGroup.Address,

            [MemberExportField.Status] = MemberExportFieldGroup.Membership,
            [MemberExportField.JoinDate] = MemberExportFieldGroup.Membership,
            [MemberExportField.LeaveDate] = MemberExportFieldGroup.Membership,
            [MemberExportField.BirthDate] = MemberExportFieldGroup.Membership,
            [MemberExportField.Notes] = MemberExportFieldGroup.Membership,

            [MemberExportField.TotalShareCount] = MemberExportFieldGroup.Shares,
            [MemberExportField.TotalShareValue] = MemberExportFieldGroup.Shares,
        };

    /// <summary>
    /// Maps each field to the resource key used as its column header in the exported file.
    /// Most reuse existing SharedResource keys; export-specific keys live in Members.resx.
    /// </summary>
    public static readonly IReadOnlyDictionary<MemberExportField, ExportFieldHeader> FieldHeader =
        new Dictionary<MemberExportField, ExportFieldHeader>
        {
            [MemberExportField.MemberNumber] = new("MemberNumber", HeaderSource.Shared),
            [MemberExportField.MemberType] = new("MemberType", HeaderSource.Shared),
            [MemberExportField.Prefix] = new("Prefix", HeaderSource.Shared),
            [MemberExportField.FirstName] = new("FirstName", HeaderSource.Shared),
            [MemberExportField.LastName] = new("LastName", HeaderSource.Shared),
            [MemberExportField.CompanyName] = new("CompanyName", HeaderSource.Shared),
            [MemberExportField.ContactPerson] = new("ContactPerson", HeaderSource.Shared),
            [MemberExportField.Email] = new("Email", HeaderSource.Shared),
            [MemberExportField.Phone] = new("Phone", HeaderSource.Shared),
            [MemberExportField.Street] = new("Street", HeaderSource.Shared),
            [MemberExportField.PostalCode] = new("PostalCode", HeaderSource.Shared),
            [MemberExportField.City] = new("City", HeaderSource.Shared),
            [MemberExportField.Country] = new("Country", HeaderSource.Shared),
            [MemberExportField.BirthDate] = new("BirthDate", HeaderSource.Shared),
            [MemberExportField.JoinDate] = new("ExportField_MemberSince", HeaderSource.Members),
            [MemberExportField.LeaveDate] = new("ExportField_LeaveDate", HeaderSource.Members),
            [MemberExportField.Status] = new("Status", HeaderSource.Shared),
            [MemberExportField.Notes] = new("Notes", HeaderSource.Shared),
            [MemberExportField.TotalShareCount] = new("ExportField_TotalShareCount", HeaderSource.Members),
            [MemberExportField.TotalShareValue] = new("ExportField_TotalShareValue", HeaderSource.Members),
        };
}

public enum HeaderSource
{
    Shared,
    Members
}

public record ExportFieldHeader(string Key, HeaderSource Source);
