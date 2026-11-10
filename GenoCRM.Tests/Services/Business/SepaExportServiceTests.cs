using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using GenoCRM.Data;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;
using Xunit;

namespace GenoCRM.Tests.Services.Business;

public class SepaExportServiceTests : IDisposable
{
    private static readonly XNamespace Ns = SepaExportService.Pain001Namespace;

    private readonly GenoDbContext _context;
    private readonly SepaExportService _service;

    public SepaExportServiceTests()
    {
        var options = new DbContextOptionsBuilder<GenoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new GenoDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SepaDebtor:Name"] = "Bürgerenergie Genossenschaft eG",
                ["SepaDebtor:Iban"] = "DE02 8709 5974 0000 1234 56",
                ["SepaDebtor:Bic"] = "GENODEF1GC1"
            })
            .Build();
        _service = new SepaExportService(_context, config, new Mock<ILogger<SepaExportService>>().Object);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Export_CreatesPain001WithOneBlockPerExecutionDateAndMarksEntriesExported()
    {
        var interest = await CreateEntryAsync("Jürgen Müller", new DateTime(2099, 12, 31), principal: 0, interest: 145.75m);
        var combined = await CreateEntryAsync("Anna Schmidt", new DateTime(2099, 4, 10), principal: 5000m, interest: 54.79m, bic: "GENODEF1GC1");

        var result = await _service.ExportLoanPaymentsAsync(new[] { interest.Id, combined.Id }, new SepaExportOptions());

        var doc = XDocument.Load(new MemoryStream(result.Content));
        var root = doc.Root!;
        Assert.Equal(Ns + "Document", root.Name);

        var header = root.Descendants(Ns + "GrpHdr").Single();
        Assert.Equal("2", header.Element(Ns + "NbOfTxs")!.Value);
        Assert.Equal("5200.54", header.Element(Ns + "CtrlSum")!.Value);

        var blocks = root.Descendants(Ns + "PmtInf").ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Equal("2099-04-10", blocks[0].Element(Ns + "ReqdExctnDt")!.Element(Ns + "Dt")!.Value);
        Assert.Equal("2099-12-31", blocks[1].Element(Ns + "ReqdExctnDt")!.Element(Ns + "Dt")!.Value);
        Assert.All(blocks, b => Assert.Equal("false", b.Element(Ns + "BtchBookg")!.Value));
        Assert.Equal("DE02870959740000123456", blocks[0].Descendants(Ns + "DbtrAcct").Single().Descendants(Ns + "IBAN").Single().Value);
        Assert.Equal("Buergerenergie Genossenschaft eG", blocks[0].Element(Ns + "Dbtr")!.Element(Ns + "Nm")!.Value);

        var tx = blocks[1].Descendants(Ns + "CdtTrfTxInf").Single();
        Assert.Equal($"GENO-{interest.Id}", tx.Descendants(Ns + "EndToEndId").Single().Value);
        Assert.Equal("145.75", tx.Descendants(Ns + "InstdAmt").Single().Value);
        Assert.Equal("EUR", tx.Descendants(Ns + "InstdAmt").Single().Attribute("Ccy")!.Value);
        Assert.Equal("Juergen Mueller", tx.Element(Ns + "Cdtr")!.Element(Ns + "Nm")!.Value);
        Assert.Null(tx.Element(Ns + "CdtrAgt"));
        Assert.StartsWith("Zinsen 2099 LS", tx.Descendants(Ns + "Ustrd").Single().Value);

        var combinedTx = blocks[0].Descendants(Ns + "CdtTrfTxInf").Single();
        Assert.Equal("5054.79", combinedTx.Descendants(Ns + "InstdAmt").Single().Value);
        Assert.Equal("GENODEF1GC1", combinedTx.Descendants(Ns + "BICFI").Single().Value);
        Assert.StartsWith("Tilgung + Zinsen LS", combinedTx.Descendants(Ns + "Ustrd").Single().Value);

        var stored = await _context.LoanPaymentPlanEntries.ToListAsync();
        Assert.All(stored, e =>
        {
            Assert.NotNull(e.ExportedAt);
            Assert.Equal(result.MessageId, e.ExportReference);
            Assert.Equal(PaymentPlanEntryStatus.Pending, e.Status);
        });
    }

    [Fact]
    public async Task Export_FixedExecutionDate_UsesSingleBlock()
    {
        var a = await CreateEntryAsync("A", new DateTime(2099, 12, 31), 0, 10m);
        var b = await CreateEntryAsync("B", new DateTime(2099, 4, 10), 100m, 1m);

        var result = await _service.ExportLoanPaymentsAsync(new[] { a.Id, b.Id }, new SepaExportOptions(new DateTime(2099, 1, 15)));

        var blocks = XDocument.Load(new MemoryStream(result.Content)).Root!.Descendants(Ns + "PmtInf").ToList();
        var block = Assert.Single(blocks);
        Assert.Equal("2099-01-15", block.Descendants(Ns + "Dt").Single().Value);
    }

    [Fact]
    public async Task Export_SkipsPaidEntriesAndEntriesWithoutIban()
    {
        var ok = await CreateEntryAsync("A", new DateTime(2099, 12, 31), 0, 10m);
        var noIban = await CreateEntryAsync("B", new DateTime(2099, 12, 31), 0, 10m, iban: "");
        var paid = await CreateEntryAsync("C", new DateTime(2099, 12, 31), 0, 10m);
        paid.Status = PaymentPlanEntryStatus.Paid;
        await _context.SaveChangesAsync();

        var result = await _service.ExportLoanPaymentsAsync(new[] { ok.Id, noIban.Id, paid.Id }, new SepaExportOptions());

        Assert.Equal(1, result.TransactionCount);
        Assert.Equal(noIban.Id, Assert.Single(result.Skipped).Id);
        Assert.Null((await _context.LoanPaymentPlanEntries.FindAsync(noIban.Id))!.ExportedAt);
        Assert.Null((await _context.LoanPaymentPlanEntries.FindAsync(paid.Id))!.ExportedAt);
    }

    [Fact]
    public void BuildPain001_PastDueDate_UsesToday()
    {
        var entry = BuildDetachedEntry(new DateTime(2020, 12, 31));
        var xml = SepaExportService.BuildPain001(new[] { entry }, new SepaDebtorSettings { Name = "X", Iban = "DE02870959740000123456" },
            "GENO-1", new DateTime(2026, 11, 10), null, new DateTime(2026, 11, 10));

        var doc = XDocument.Load(new MemoryStream(xml));
        Assert.Equal("2026-11-10", doc.Root!.Descendants(Ns + "Dt").Single().Value);
        Assert.Equal("NOTPROVIDED", doc.Root.Descendants(Ns + "DbtrAgt").Single().Descendants(Ns + "Id").Single().Value);
    }

    [Theory]
    [InlineData("Müller & Söhne GbR", 70, "Mueller + Soehne GbR")]
    [InlineData("Café Łódź", 70, "Cafe odz")]
    [InlineData("Straße", 4, "Stra")]
    [InlineData("a  _ b", 70, "a b")]
    public void Sanitize_RestrictsToSepaCharset(string input, int max, string expected)
    {
        Assert.Equal(expected, SepaExportService.Sanitize(input, max));
    }

    private static LoanPaymentPlanEntry BuildDetachedEntry(DateTime dueDate) => new()
    {
        Id = 1,
        DueDate = dueDate,
        InterestAmount = 10m,
        TotalAmount = 10m,
        LoanPaymentPlan = new LoanPaymentPlan
        {
            LoanSubscription = new LoanSubscription
            {
                SubscriptionNumber = "LS0001",
                BankAccountHolder = "Test",
                IBAN = "DE89370400440532013000",
                Member = new Member { FirstName = "T", LastName = "T" },
                LoanOffer = new LoanOffer { LoanProject = new LoanProject { Title = "P" } }
            }
        }
    };

    private async Task<LoanPaymentPlanEntry> CreateEntryAsync(string holder, DateTime dueDate, decimal principal, decimal interest,
        string iban = "DE89370400440532013000", string? bic = null)
    {
        var offer = await _context.LoanOffers.FirstOrDefaultAsync() ?? new LoanOffer
        {
            LoanProject = new LoanProject { Title = "Stausee Oberwald", ProjectNumber = "P1", FinancingAmount = 1m },
            Title = "2 Jahre",
            InterestRate = 0.04m,
            TermMonths = 24,
            PaymentInterval = PaymentInterval.Annual,
            RepaymentType = RepaymentType.BulletLoan
        };
        var n = await _context.LoanSubscriptions.CountAsync() + 1;
        var sub = new LoanSubscription
        {
            LoanOffer = offer,
            Member = new Member { MemberNumber = $"M{n}", FirstName = "F", LastName = $"L{n}", Email = $"m{n}@example.com" },
            SubscriptionNumber = $"LS{n:0000}",
            Amount = 5000m,
            PaidInDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            BankAccountHolder = holder,
            IBAN = iban,
            BIC = bic
        };
        var entry = new LoanPaymentPlanEntry
        {
            PeriodNumber = 1,
            DueDate = dueDate,
            PrincipalAmount = principal,
            InterestAmount = interest,
            TotalAmount = principal + interest,
            InterestPeriodStart = new DateTime(dueDate.Year, 1, 1)
        };
        _context.LoanPaymentPlans.Add(new LoanPaymentPlan { LoanSubscription = sub, Entries = { entry } });
        await _context.SaveChangesAsync();
        return entry;
    }
}
