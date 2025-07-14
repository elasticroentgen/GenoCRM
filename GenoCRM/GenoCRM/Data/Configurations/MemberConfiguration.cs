using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        
        builder.HasKey(m => m.Id);
        
        builder.Property(m => m.MemberNumber)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(m => m.FirstName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(m => m.LastName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(m => m.Email)
            .HasMaxLength(200);
        
        builder.Property(m => m.Phone)
            .HasMaxLength(20);
        
        builder.Property(m => m.Street)
            .HasMaxLength(200);
        
        builder.Property(m => m.PostalCode)
            .HasMaxLength(10);
        
        builder.Property(m => m.City)
            .HasMaxLength(100);
        
        builder.Property(m => m.Country)
            .HasMaxLength(100);
        
        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(m => m.JoinDate)
            .IsRequired();
        
        builder.Property(m => m.CreatedAt)
            .IsRequired();
        
        builder.Property(m => m.UpdatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasMany(m => m.Shares)
            .WithOne(s => s.Member)
            .HasForeignKey(s => s.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(m => m.Payments)
            .WithOne(p => p.Member)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(m => m.Dividends)
            .WithOne(d => d.Member)
            .HasForeignKey(d => d.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(m => m.SubordinatedLoans)
            .WithOne(l => l.Member)
            .HasForeignKey(l => l.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(m => m.Documents)
            .WithOne(d => d.Member)
            .HasForeignKey(d => d.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Computed columns are ignored for EF
        builder.Ignore(m => m.FullName);
        builder.Ignore(m => m.TotalShareValue);
        builder.Ignore(m => m.TotalShareCount);
    }
}