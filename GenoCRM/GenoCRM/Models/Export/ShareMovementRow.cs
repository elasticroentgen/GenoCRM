namespace GenoCRM.Models.Export;

public class ShareMovementRow
{
    public DateTime Date { get; init; }
    public ShareMovementType Type { get; init; }
    public string MemberNumber { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public int? Quantity { get; init; }
    public decimal? NominalValue { get; init; }
    public decimal? TotalValue { get; init; }
    public string? CertificateNumber { get; init; }
    public string? CounterMemberNumber { get; init; }
    public string? CounterMemberName { get; init; }
    public string? Notes { get; init; }
}
