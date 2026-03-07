using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class LoanPaymentPlanEntryConfiguration : IEntityTypeConfiguration<LoanPaymentPlanEntry>
{
    public void Configure(EntityTypeBuilder<LoanPaymentPlanEntry> builder)
    {
        builder.ToTable("LoanPaymentPlanEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PeriodNumber)
            .IsRequired();

        builder.Property(e => e.DueDate)
            .IsRequired();

        builder.Property(e => e.PrincipalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.InterestAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.RemainingBalance)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.LoanPaymentPlan)
            .WithMany(p => p.Entries)
            .HasForeignKey(e => e.LoanPaymentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
