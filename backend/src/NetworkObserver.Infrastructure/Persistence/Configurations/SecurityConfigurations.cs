using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.RoleId).HasName("roles_pkey");
        builder.Property(r => r.RoleId).HasColumnName("role_id").ValueGeneratedNever();
        builder.Property(r => r.RoleCode).HasColumnName("role_code").HasMaxLength(50).IsRequired();
        builder.Property(r => r.RoleName).HasColumnName("role_name").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description");

        builder.HasIndex(r => r.RoleCode).IsUnique();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.UserId).HasName("users_pkey");
        builder.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedOnAdd();
        builder.Property(u => u.SiteId).HasColumnName("site_id").HasDefaultValue(1).IsRequired();
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasOne(u => u.Site)
            .WithMany(s => s.Users)
            .HasForeignKey(u => u.SiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_site");

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_role");
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(al => al.LogId).HasName("audit_logs_pkey");
        builder.Property(al => al.LogId).HasColumnName("log_id").ValueGeneratedOnAdd();
        builder.Property(al => al.UserId).HasColumnName("user_id");
        builder.Property(al => al.SiteId).HasColumnName("site_id").HasDefaultValue(1).IsRequired();
        builder.Property(al => al.ActionCode).HasColumnName("action_code").HasMaxLength(50).IsRequired();
        builder.Property(al => al.TargetEntity).HasColumnName("target_entity").HasMaxLength(50).IsRequired();
        builder.Property(al => al.TargetId).HasColumnName("target_id").HasMaxLength(50);
        builder.Property(al => al.Details).HasColumnName("details").HasColumnType("jsonb");
        builder.Property(al => al.IpAddress).HasColumnName("ip_address").HasMaxLength(45).IsRequired();
        builder.Property(al => al.LoggedAt).HasColumnName("logged_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(al => al.LoggedAt).HasDatabaseName("idx_audit_time");
        builder.HasIndex(al => al.UserId).HasDatabaseName("idx_audit_user");
        builder.HasIndex(al => al.ActionCode).HasDatabaseName("idx_audit_action");

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_audit_user");

        builder.HasOne(al => al.Site)
            .WithMany(s => s.AuditLogs)
            .HasForeignKey(al => al.SiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_site");
    }
}
