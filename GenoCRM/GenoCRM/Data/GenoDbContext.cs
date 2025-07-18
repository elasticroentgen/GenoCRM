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
    public DbSet<ShareTransfer> ShareTransfers { get; set; }
    public DbSet<ShareApproval> ShareApprovals { get; set; }
    
    // Messaging
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageTemplate> MessageTemplates { get; set; }
    public DbSet<MessagePreference> MessagePreferences { get; set; }
    public DbSet<MessageCampaign> MessageCampaigns { get; set; }
    
    // Authentication and Authorization
    public DbSet<User> Users { get; set; }
    public DbSet<UserGroup> UserGroups { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }
    
    // Audit Trail
    public DbSet<AuditLog> AuditLogs { get; set; }

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
        
        // Configure messaging relationships
        ConfigureMessagingRelationships(modelBuilder);
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
            .HasIndex(d => d.ShareId);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Type);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Status);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.CreatedAt);
        
        modelBuilder.Entity<Document>()
            .HasIndex(d => d.ExpirationDate);
            
        // Message indexes
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.MemberId);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Type);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Channel);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Status);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.CreatedAt);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.SentAt);
        
        // MessageTemplate indexes
        modelBuilder.Entity<MessageTemplate>()
            .HasIndex(t => t.Type);
        
        modelBuilder.Entity<MessageTemplate>()
            .HasIndex(t => t.Channel);
        
        modelBuilder.Entity<MessageTemplate>()
            .HasIndex(t => t.IsActive);
        
        // MessagePreference indexes
        modelBuilder.Entity<MessagePreference>()
            .HasIndex(p => new { p.MemberId, p.Type })
            .IsUnique();
        
        // MessageCampaign indexes
        modelBuilder.Entity<MessageCampaign>()
            .HasIndex(c => c.Type);
        
        modelBuilder.Entity<MessageCampaign>()
            .HasIndex(c => c.Status);
        
        modelBuilder.Entity<MessageCampaign>()
            .HasIndex(c => c.ScheduledAt);
            
        // AuditLog indexes
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.UserName);
        
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.EntityType);
        
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.EntityType, a.EntityId });
        
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp);
    }
    
    private static void ConfigureMessagingRelationships(ModelBuilder modelBuilder)
    {
        // Message -> Member relationship
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Member)
            .WithMany()
            .HasForeignKey(m => m.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Message -> User relationship
        modelBuilder.Entity<Message>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // MessagePreference -> Member relationship
        modelBuilder.Entity<MessagePreference>()
            .HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
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
                       e.Entity is SubordinatedLoan || e.Entity is Document ||
                       e.Entity is ShareTransfer || e.Entity is ShareApproval ||
                       e.Entity is Message || e.Entity is MessageTemplate ||
                       e.Entity is MessagePreference || e.Entity is MessageCampaign ||
                       e.Entity is AuditLog)
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