using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class CooperativeShareConfiguration : IEntityTypeConfiguration<CooperativeShare>
{
    public void Configure(EntityTypeBuilder<CooperativeShare> builder)
    {
        builder.ToTable("CooperativeShares");
        
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.CertificateNumber)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(s => s.Quantity)
            .IsRequired();
        
        builder.Property(s => s.NominalValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(s => s.Value)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(s => s.IssueDate)
            .IsRequired();
        
        builder.Property(s => s.CreatedAt)
            .IsRequired();
        
        builder.Property(s => s.UpdatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(s => s.Member)
            .WithMany(m => m.Shares)
            .HasForeignKey(s => s.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(s => s.Payments)
            .WithOne(p => p.Share)
            .HasForeignKey(p => p.ShareId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasMany(s => s.Dividends)
            .WithOne(d => d.Share)
            .HasForeignKey(d => d.ShareId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Computed columns are ignored for EF
        builder.Ignore(s => s.TotalValue);
        builder.Ignore(s => s.IsFullyPaid);
        builder.Ignore(s => s.PaidAmount);
        builder.Ignore(s => s.OutstandingAmount);
    }
}