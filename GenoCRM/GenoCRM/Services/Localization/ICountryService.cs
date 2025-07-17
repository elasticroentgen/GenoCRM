namespace GenoCRM.Services.Localization;

public interface ICountryService
{
    Dictionary<string, string> GetCountries();
    string GetCountryName(string countryCode);
    string GetDefaultCountryCode();
}

public class CountryInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}