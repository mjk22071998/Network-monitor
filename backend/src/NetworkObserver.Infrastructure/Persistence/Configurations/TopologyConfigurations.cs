using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");

        builder.HasKey(s => s.SiteId).HasName("sites_pkey");
        builder.Property(s => s.SiteId).HasColumnName("site_id").ValueGeneratedOnAdd();
        builder.Property(s => s.SiteName).HasColumnName("site_name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.FacilityCode).HasColumnName("facility_code").HasMaxLength(50);
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(s => s.SiteName).IsUnique();
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.GroupId).HasName("groups_pkey");
        builder.Property(g => g.GroupId).HasColumnName("group_id").ValueGeneratedOnAdd();
        builder.Property(g => g.SiteId).HasColumnName("site_id").HasDefaultValue(1);
        builder.Property(g => g.ParentGroupId).HasColumnName("parent_group_id");
        builder.Property(g => g.GroupName).HasColumnName("group_name").HasMaxLength(100).IsRequired();
        builder.Property(g => g.Description).HasColumnName("description");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(g => g.Site)
            .WithMany(s => s.Groups)
            .HasForeignKey(g => g.SiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_group_site");

        builder.HasOne(g => g.ParentGroup)
            .WithMany(g => g.Subgroups)
            .HasForeignKey(g => g.ParentGroupId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_group_parent");

        builder.HasIndex(g => new { g.SiteId, g.ParentGroupId, g.GroupName })
            .IsUnique()
            .HasDatabaseName("uq_site_group_name");

        builder.HasIndex(g => g.ParentGroupId).HasDatabaseName("idx_groups_parent");
        builder.HasIndex(g => g.SiteId).HasDatabaseName("idx_groups_site");
    }
}

public class DeviceTypeConfiguration : IEntityTypeConfiguration<DeviceType>
{
    public void Configure(EntityTypeBuilder<DeviceType> builder)
    {
        builder.ToTable("device_types");

        builder.HasKey(dt => dt.TypeId).HasName("device_types_pkey");
        builder.Property(dt => dt.TypeId).HasColumnName("type_id").ValueGeneratedOnAdd();
        builder.Property(dt => dt.TypeCode).HasColumnName("type_code").HasMaxLength(30).IsRequired();

        builder.HasIndex(dt => dt.TypeCode).IsUnique();
    }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.DeviceId).HasName("devices_pkey");
        builder.Property(d => d.DeviceId).HasColumnName("device_id").ValueGeneratedOnAdd();
        builder.Property(d => d.MacAddress).HasColumnName("mac_address").HasMaxLength(17).IsRequired();
        builder.Property(d => d.PrimaryIp).HasColumnName("primary_ip").HasMaxLength(45).IsRequired();
        builder.Property(d => d.DisplayName).HasColumnName("display_name").HasMaxLength(150).IsRequired();
        builder.Property(d => d.TypeId).HasColumnName("type_id").IsRequired();
        builder.Property(d => d.GroupId).HasColumnName("group_id").HasDefaultValue(1).IsRequired();
        builder.Property(d => d.ParentDeviceId).HasColumnName("parent_device_id");
        builder.Property(d => d.Zone).HasColumnName("zone").HasMaxLength(100);
        builder.Property(d => d.Vendor).HasColumnName("vendor").HasMaxLength(100);
        builder.Property(d => d.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(d => d.IsMonitored).HasColumnName("is_monitored").HasDefaultValue(true).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(d => d.MacAddress).IsUnique().HasDatabaseName("devices_mac_address_key");
        builder.HasIndex(d => d.PrimaryIp).HasDatabaseName("idx_devices_ip");
        builder.HasIndex(d => d.MacAddress).HasDatabaseName("idx_devices_mac");
        builder.HasIndex(d => d.ParentDeviceId).HasDatabaseName("idx_devices_parent");
        builder.HasIndex(d => d.GroupId).HasDatabaseName("idx_devices_group");
        builder.HasIndex(d => d.IsMonitored).HasDatabaseName("idx_devices_monitored");

        builder.HasOne(d => d.DeviceType)
            .WithMany(dt => dt.Devices)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_device_type");

        builder.HasOne(d => d.Group)
            .WithMany(g => g.Devices)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_device_group");

        builder.HasOne(d => d.ParentDevice)
            .WithMany(d => d.ChildDevices)
            .HasForeignKey(d => d.ParentDeviceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_device_parent");
    }
}

public class PhysicalLayoutConfiguration : IEntityTypeConfiguration<PhysicalLayout>
{
    public void Configure(EntityTypeBuilder<PhysicalLayout> builder)
    {
        builder.ToTable("physical_layouts");

        builder.HasKey(pl => pl.LayoutId).HasName("physical_layouts_pkey");
        builder.Property(pl => pl.LayoutId).HasColumnName("layout_id").ValueGeneratedOnAdd();
        builder.Property(pl => pl.SiteId).HasColumnName("site_id").HasDefaultValue(1);
        builder.Property(pl => pl.LayoutSlug).HasColumnName("layout_slug").HasMaxLength(100).IsRequired();
        builder.Property(pl => pl.FloorName).HasColumnName("floor_name").HasMaxLength(150).IsRequired();
        builder.Property(pl => pl.LayoutFilePath).HasColumnName("layout_file_path").HasMaxLength(255).IsRequired();
        builder.Property(pl => pl.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(pl => pl.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(pl => pl.LayoutSlug).IsUnique();
        builder.HasIndex(pl => pl.SiteId).HasDatabaseName("idx_layouts_site");

        builder.HasOne(pl => pl.Site)
            .WithMany(s => s.PhysicalLayouts)
            .HasForeignKey(pl => pl.SiteId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_layout_site");
    }
}
