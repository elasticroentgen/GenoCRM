using Microsoft.EntityFrameworkCore;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data;

public class GenoDbContext : DbContext
{
    public GenoDbContext(DbContextOptions<GenoDbContext> options) : base(options)
    {
    }

    public DbSet<Member> Members { get; set; }
    public DbSet<CooperativeShare> CooperativeShares { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Dividend> Dividends { get; set; }
    public DbSet<SubordinatedLoan> SubordinatedLoans { get; set; }
    public DbSet<LoanInterest> LoanInterests { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentVersion> DocumentVersions { get; set; }
    
    // Authentication and Authorization
    public DbSet<User> Users { get; set; }
    public DbSet<UserGroup> UserGroups { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GenoDbContext).Assembly);

        // Global query filters
        modelBuilder.Entity<Member>().HasQueryFilter(m => m.Status != MemberStatus.Terminated);
        modelBuilder.Entity<Document>().HasQueryFilter(d => d.Status != DocumentStatus.Deleted);

        // Index configurations
        ConfigureIndexes(modelBuilder);
    }

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Member indexes
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.MemberNumber)
            .IsUnique();
        
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Email);
        
        modelBuilder.Entity<Member>()
            .HasIndex(m => new { m.FirstName, m.LastName });
        
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Status);

        // CooperativeShare indexes
        modelBuilder.Entity<CooperativeShare>()
            .HasIndex(s => s.CertificateNumber)
            .IsUnique();
        
        modelBuilder.Entity<CooperativeShare>()
            .HasIndex(s => s.MemberId);
        
        modelBuilder.Entity<CooperativeShare>()
            .HasIndex(s => s.Status);

        // Payment indexes
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.PaymentNumber)
            .IsUnique();
        
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.MemberId);
        
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.PaymentDate);
        
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Status);
        
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Type);

        // Dividend indexes
        modelBuilder.Entity<Dividend>()
            .HasIndex(d => new { d.MemberId, d.FiscalYear });
        
        modelBuilder.Entity<Dividend>()
            .HasIndex(d => d.FiscalYear);
        
        modelBuilder.Entity<Dividend>()
            .HasIndex(d => d.Status);

        // SubordinatedLoan indexes
        modelBuilder.Entity<SubordinatedLoan>()
            .HasIndex(l => l.LoanNumber)
            .IsUnique();
        
        modelBuilder.Entity<SubordinatedLoan>()
            .HasIndex(l => l.MemberId);
        
        modelBuilder.Entity<SubordinatedLoan>()
            .HasIndex(l => l.Status);
        
        modelBuilder.Entity<SubordinatedLoan>()
            .HasIndex(l => l.MaturityDate);

        // Document indexes
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.MemberId);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Type);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Status);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.CreatedAt);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.ExpirationDate);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Member || e.Entity is CooperativeShare || 
                       e.Entity is Payment || e.Entity is Dividend || 
                       e.Entity is SubordinatedLoan || e.Entity is Document)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null)
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
            }
            
            if (entry.Property("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}