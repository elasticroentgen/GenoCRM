using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class LoanSubscriptionConfiguration : IEntityTypeConfiguration<LoanSubscription>
{
    public void Configure(EntityTypeBuilder<LoanSubscription> builder)
    {
        builder.ToTable("LoanSubscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SubscriptionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.SubscriptionDate)
            .IsRequired();

        builder.Property(s => s.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.BankAccountHolder)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.IBAN)
            .IsRequired()
            .HasMaxLength(34);

        builder.Property(s => s.BIC)
            .IsRequired()
            .HasMaxLength(11);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(s => s.LoanOffer)
            .WithMany(o => o.Subscriptions)
            .HasForeignKey(s => s.LoanOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Member)
            .WithMany(m => m.LoanSubscriptions)
            .HasForeignKey(s => s.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.PaymentPlan)
            .WithOne(p => p.LoanSubscription)
            .HasForeignKey<LoanPaymentPlan>(p => p.LoanSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
