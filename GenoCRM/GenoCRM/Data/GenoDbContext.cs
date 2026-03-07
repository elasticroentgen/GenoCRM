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
    public DbSet<LoanProject> LoanProjects { get; set; }
    public DbSet<LoanOffer> LoanOffers { get; set; }
    public DbSet<LoanSubscription> LoanSubscriptions { get; set; }
    public DbSet<LoanPaymentPlan> LoanPaymentPlans { get; set; }
    public DbSet<LoanPaymentPlanEntry> LoanPaymentPlanEntries { get; set; }
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
        
        // Matching query filters for entities with Member relationships
        modelBuilder.Entity<CooperativeShare>().HasQueryFilter(cs => cs.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<Dividend>().HasQueryFilter(d => d.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<Message>().HasQueryFilter(m => m.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<MessagePreference>().HasQueryFilter(mp => mp.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => p.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<ShareApproval>().HasQueryFilter(sa => sa.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<ShareTransfer>().HasQueryFilter(st => st.FromMember.Status != MemberStatus.Terminated && st.ToMember.Status != MemberStatus.Terminated);
        modelBuilder.Entity<LoanSubscription>().HasQueryFilter(ls => ls.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<LoanPaymentPlan>().HasQueryFilter(lp => lp.LoanSubscription.Member.Status != MemberStatus.Terminated);
        modelBuilder.Entity<LoanPaymentPlanEntry>().HasQueryFilter(le => le.LoanPaymentPlan.LoanSubscription.Member.Status != MemberStatus.Terminated);
        
        // Matching query filter for DocumentVersion with Document relationship
        modelBuilder.Entity<DocumentVersion>().HasQueryFilter(dv => dv.Document.Status != DocumentStatus.Deleted);

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

        // LoanProject indexes
        modelBuilder.Entity<LoanProject>()
            .HasIndex(p => p.ProjectNumber)
            .IsUnique();

        modelBuilder.Entity<LoanProject>()
            .HasIndex(p => p.Status);

        // LoanOffer indexes
        modelBuilder.Entity<LoanOffer>()
            .HasIndex(o => o.LoanProjectId);

        modelBuilder.Entity<LoanOffer>()
            .HasIndex(o => o.Status);

        // LoanSubscription indexes
        modelBuilder.Entity<LoanSubscription>()
            .HasIndex(s => s.SubscriptionNumber)
            .IsUnique();

        modelBuilder.Entity<LoanSubscription>()
            .HasIndex(s => s.MemberId);

        modelBuilder.Entity<LoanSubscription>()
            .HasIndex(s => s.LoanOfferId);

        modelBuilder.Entity<LoanSubscription>()
            .HasIndex(s => s.Status);

        // LoanPaymentPlan indexes
        modelBuilder.Entity<LoanPaymentPlan>()
            .HasIndex(p => p.LoanSubscriptionId)
            .IsUnique();

        // LoanPaymentPlanEntry indexes
        modelBuilder.Entity<LoanPaymentPlanEntry>()
            .HasIndex(e => e.LoanPaymentPlanId);

        modelBuilder.Entity<LoanPaymentPlanEntry>()
            .HasIndex(e => e.DueDate);

        modelBuilder.Entity<LoanPaymentPlanEntry>()
            .HasIndex(e => e.Status);

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
                       e.Entity is LoanProject || e.Entity is LoanOffer ||
                       e.Entity is LoanSubscription || e.Entity is LoanPaymentPlan ||
                       e.Entity is LoanPaymentPlanEntry || e.Entity is Document ||
                       e.Entity is ShareTransfer || e.Entity is ShareApproval ||
                       e.Entity is Message || e.Entity is MessageTemplate ||
                       e.Entity is MessagePreference || e.Entity is MessageCampaign ||
                       e.Entity is AuditLog)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            // Convert all DateTime properties with Unspecified Kind to UTC
            foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(DateTime) || p.Metadata.ClrType == typeof(DateTime?)))
            {
                if (property.CurrentValue is DateTime dateTime && dateTime.Kind == DateTimeKind.Unspecified)
                {
                    property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                }
            }
            
            // Handle timestamp properties
            var entityType = entry.Entity.GetType();
            
            if (entry.State == EntityState.Added)
            {
                var createdAtProperty = entityType.GetProperty("CreatedAt");
                if (createdAtProperty != null)
                {
                    var propEntry = entry.Property("CreatedAt");
                    if (propEntry.CurrentValue == null)
                        propEntry.CurrentValue = DateTime.UtcNow;
                }
            }
            
            var updatedAtProperty = entityType.GetProperty("UpdatedAt");
            if (updatedAtProperty != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}