using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class LoanPaymentPlanConfiguration : IEntityTypeConfiguration<LoanPaymentPlan>
{
    public void Configure(EntityTypeBuilder<LoanPaymentPlan> builder)
    {
        builder.ToTable("LoanPaymentPlans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.GeneratedAt)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(p => p.LoanSubscription)
            .WithOne(s => s.PaymentPlan)
            .HasForeignKey<LoanPaymentPlan>(p => p.LoanSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Entries)
            .WithOne(e => e.LoanPaymentPlan)
            .HasForeignKey(e => e.LoanPaymentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
