using System.Reflection;

namespace GenoCRM.Services.Business;

public class IbanBicLookupService : IIbanBicLookupService
{
    private readonly Dictionary<string, string> _blzToBic;
    private readonly ILogger<IbanBicLookupService> _logger;

    public IbanBicLookupService(ILogger<IbanBicLookupService> logger)
    {
        _logger = logger;
        _blzToBic = LoadBundesbankData();
        _logger.LogInformation("IbanBicLookupService loaded {Count} BLZ→BIC entries", _blzToBic.Count);
    }

    public string? Lookup(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;

        var normalized = new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (normalized.Length < 12 || !normalized.StartsWith("DE")) return null;

        var blz = normalized.Substring(4, 8);
        return _blzToBic.TryGetValue(blz, out var bic) ? bic : null;
    }

    private static Dictionary<string, string> LoadBundesbankData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("blz.txt", StringComparison.OrdinalIgnoreCase));

        var result = new Dictionary<string, string>();
        if (resourceName == null) return result;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return result;
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length < 150) continue;
            if (line.StartsWith("#")) continue;

            // Bundesbank BLZ-Verzeichnis fixed-width format:
            //   pos  1- 8: BLZ (8 digits)
            //   pos      9: Merkmal (1 = BLZ aktiv)
            //   pos 140-150: BIC (11 chars, padded)
            var blz = line.Substring(0, 8).Trim();
            var marker = line[8];
            if (marker != '1') continue;
            var bic = line.Substring(139, 11).Trim();

            if (blz.Length == 8 && !string.IsNullOrEmpty(bic))
            {
                result[blz] = bic;
            }
        }

        return result;
    }
}
