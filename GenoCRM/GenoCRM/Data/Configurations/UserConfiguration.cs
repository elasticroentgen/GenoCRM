using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GenoCRM.Models.Domain;

namespace GenoCRM.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.NextcloudUserId)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(u => u.DisplayName)
            .HasMaxLength(200);
        
        builder.Property(u => u.CreatedAt)
            .IsRequired();
        
        builder.Property(u => u.UpdatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(u => u.NextcloudUserId)
            .IsUnique();
        
        builder.HasIndex(u => u.Email);
        
        // Relationships
        builder.HasMany(u => u.UserGroups)
            .WithOne(ug => ug.User)
            .HasForeignKey(ug => ug.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(u => u.UserPermissions)
            .WithOne(up => up.User)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Computed columns are ignored for EF
        builder.Ignore(u => u.FullName);
        builder.Ignore(u => u.GroupNames);
    }
}

public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> builder)
    {
        builder.ToTable("UserGroups");
        
        builder.HasKey(ug => ug.Id);
        
        builder.Property(ug => ug.GroupName)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(ug => ug.Description)
            .HasMaxLength(200);
        
        builder.Property(ug => ug.CreatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(ug => new { ug.UserId, ug.GroupName })
            .IsUnique();
        
        // Relationships
        builder.HasOne(ug => ug.User)
            .WithMany(u => u.UserGroups)
            .HasForeignKey(ug => ug.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");
        
        builder.HasKey(up => up.Id);
        
        builder.Property(up => up.Permission)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(up => up.GrantedBy)
            .HasMaxLength(100);
        
        builder.Property(up => up.CreatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(up => new { up.UserId, up.Permission })
            .IsUnique();
        
        builder.HasIndex(up => up.Permission);
        
        // Relationships
        builder.HasOne(up => up.User)
            .WithMany(u => u.UserPermissions)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

