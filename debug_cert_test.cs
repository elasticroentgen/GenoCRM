using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;

public class DebugCertTest
{
    public static async Task Main()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new GenoDbContext(options);
        var mockLogger = new Mock<ILogger<ShareService>>();
        var inMemorySettings = new Dictionary<string, string>
        {
            {"CooperativeSettings:ShareDenomination", "250.00"},
            {"CooperativeSettings:MaxSharesPerMember", "100"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var shareService = new ShareService(context, mockLogger.Object, configuration);

        // Add test shares
        var existingShares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                Id = 1,
                MemberId = 1,
                CertificateNumber = "CERT001",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active
            },
            new CooperativeShare
            {
                Id = 2,
                MemberId = 1,
                CertificateNumber = "CERT005",
                Quantity = 1,
                NominalValue = 250.00m,
                Value = 250.00m,
                Status = ShareStatus.Active
            }
        };

        context.CooperativeShares.AddRange(existingShares);
        await context.SaveChangesAsync();

        // Debug: Check if shares exist
        var allShares = await context.CooperativeShares.ToListAsync();
        Console.WriteLine($"Found {allShares.Count} shares in database");
        foreach (var share in allShares)
        {
            Console.WriteLine($"Share: {share.CertificateNumber}");
        }

        // Generate certificate number
        var result = await shareService.GenerateNextCertificateNumberAsync();
        Console.WriteLine($"Generated certificate number: {result}");
    }
}
