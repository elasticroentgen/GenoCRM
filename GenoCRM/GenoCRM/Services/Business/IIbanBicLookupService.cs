namespace GenoCRM.Services.Business;

public interface IIbanBicLookupService
{
    string? Lookup(string? iban);
}
