using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class LoanOfferConfiguration : IEntityTypeConfiguration<LoanOffer>
{
    public void Configure(EntityTypeBuilder<LoanOffer> builder)
    {
        builder.ToTable("LoanOffers");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.InterestRate)
            .IsRequired()
            .HasColumnType("decimal(5,4)");

        builder.Property(o => o.TermMonths)
            .IsRequired();

        builder.Property(o => o.PaymentInterval)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.RepaymentType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.MinSubscriptionAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.MaxSubscriptionAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(o => o.LoanProject)
            .WithMany(p => p.LoanOffers)
            .HasForeignKey(o => o.LoanProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Subscriptions)
            .WithOne(s => s.LoanOffer)
            .HasForeignKey(s => s.LoanOfferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
