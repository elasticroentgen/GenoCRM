using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.PaymentNumber)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
        
        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(p => p.Method)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(p => p.Reference)
            .HasMaxLength(100);
        
        builder.Property(p => p.PaymentDate)
            .IsRequired();
        
        builder.Property(p => p.CreatedAt)
            .IsRequired();
        
        builder.Property(p => p.UpdatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(p => p.Member)
            .WithMany(m => m.Payments)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(p => p.Share)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.ShareId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(p => p.SubordinatedLoan)
            .WithMany(l => l.Payments)
            .HasForeignKey(p => p.SubordinatedLoanId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}