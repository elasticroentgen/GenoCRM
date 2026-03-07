using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class LoanProjectConfiguration : IEntityTypeConfiguration<LoanProject>
{
    public void Configure(EntityTypeBuilder<LoanProject> builder)
    {
        builder.ToTable("LoanProjects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.ProjectNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.FinancingAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.StartDate)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasMany(p => p.LoanOffers)
            .WithOne(o => o.LoanProject)
            .HasForeignKey(o => o.LoanProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Computed columns
        builder.Ignore(p => p.TotalSubscribed);
        builder.Ignore(p => p.FinancingProgress);
    }
}
