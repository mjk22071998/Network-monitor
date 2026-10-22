using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Configurations;

public class MonitoringConfigConfiguration : IEntityTypeConfiguration<MonitoringConfig>
{
    public void Configure(EntityTypeBuilder<MonitoringConfig> builder)
    {
        builder.ToTable("monitoring_config");

        builder.HasKey(mc => mc.ConfigId).HasName("monitoring_config_pkey");
        builder.Property(mc => mc.ConfigId).HasColumnName("config_id").HasDefaultValue(1);
        builder.Property(mc => mc.SiteId).HasColumnName("site_id").HasDefaultValue(1);
        builder.Property(mc => mc.ReachabilityIntervalSec).HasColumnName("reachability_interval_sec").HasDefaultValue(5).IsRequired();
        builder.Property(mc => mc.ReachabilityTimeoutMs).HasColumnName("reachability_timeout_ms").HasDefaultValue(2000).IsRequired();
        builder.Property(mc => mc.HealthIntervalSec).HasColumnName("health_interval_sec").HasDefaultValue(30).IsRequired();
        builder.Property(mc => mc.HealthTimeoutMs).HasColumnName("health_timeout_ms").HasDefaultValue(5000).IsRequired();
        builder.Property(mc => mc.CascadeSuppressionEnabled).HasColumnName("cascade_suppression_enabled").HasDefaultValue(true).IsRequired();
        builder.Property(mc => mc.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(mc => mc.Site)
            .WithOne(s => s.MonitoringConfig)
            .HasForeignKey<MonitoringConfig>(mc => mc.SiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mon_config_site");
    }
}

public class ReachabilityStatusTypeConfiguration : IEntityTypeConfiguration<ReachabilityStatusType>
{
    public void Configure(EntityTypeBuilder<ReachabilityStatusType> builder)
    {
        builder.ToTable("reachability_status_types");

        builder.HasKey(rst => rst.StatusId).HasName("reachability_status_types_pkey");
        builder.Property(rst => rst.StatusId).HasColumnName("status_id").ValueGeneratedNever();
        builder.Property(rst => rst.StatusCode).HasColumnName("status_code").HasMaxLength(30).IsRequired();

        builder.HasIndex(rst => rst.StatusCode).IsUnique();
    }
}

public class ReachabilitySampleConfiguration : IEntityTypeConfiguration<ReachabilitySample>
{
    public void Configure(EntityTypeBuilder<ReachabilitySample> builder)
    {
        builder.ToTable("reachability_samples");

        // Composite primary key required by PostgreSQL range partitioning
        builder.HasKey(rs => new { rs.SampleId, rs.RecordedAt }).HasName("pk_reachability_samples");
        builder.Property(rs => rs.SampleId).HasColumnName("sample_id").ValueGeneratedOnAdd();
        builder.Property(rs => rs.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(rs => rs.RecordedAt).HasColumnName("recorded_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(rs => rs.StatusId).HasColumnName("status_id").IsRequired();
        builder.Property(rs => rs.LatencyUs).HasColumnName("latency_us");
        builder.Property(rs => rs.LossPercentage).HasColumnName("loss_percentage").HasDefaultValue(0.0f).IsRequired();
        builder.Property(rs => rs.NtpOffsetUs).HasColumnName("ntp_offset_us");
        builder.Property(rs => rs.IsSuppressed).HasColumnName("is_suppressed").HasDefaultValue(false).IsRequired();

        builder.HasOne(rs => rs.Device)
            .WithMany(d => d.ReachabilitySamples)
            .HasForeignKey(rs => rs.DeviceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_reach_device");

        builder.HasOne(rs => rs.StatusType)
            .WithMany(st => st.Samples)
            .HasForeignKey(rs => rs.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_reach_status");

        builder.HasIndex(rs => new { rs.DeviceId, rs.RecordedAt }).HasDatabaseName("idx_reach_device_time");
        builder.HasIndex(rs => rs.RecordedAt).HasDatabaseName("idx_reach_time");
    }
}

public class HealthStatusTypeConfiguration : IEntityTypeConfiguration<HealthStatusType>
{
    public void Configure(EntityTypeBuilder<HealthStatusType> builder)
    {
        builder.ToTable("health_status_types");

        builder.HasKey(hst => hst.StatusId).HasName("health_status_types_pkey");
        builder.Property(hst => hst.StatusId).HasColumnName("status_id").ValueGeneratedNever();
        builder.Property(hst => hst.StatusCode).HasColumnName("status_code").HasMaxLength(30).IsRequired();

        builder.HasIndex(hst => hst.StatusCode).IsUnique();
    }
}

public class HealthSampleConfiguration : IEntityTypeConfiguration<HealthSample>
{
    public void Configure(EntityTypeBuilder<HealthSample> builder)
    {
        builder.ToTable("health_samples");

        // Composite primary key required by PostgreSQL range partitioning
        builder.HasKey(hs => new { hs.SampleId, hs.RecordedAt }).HasName("pk_health_samples");
        builder.Property(hs => hs.SampleId).HasColumnName("sample_id").ValueGeneratedOnAdd();
        builder.Property(hs => hs.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(hs => hs.RecordedAt).HasColumnName("recorded_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(hs => hs.StatusId).HasColumnName("status_id").IsRequired();
        builder.Property(hs => hs.CpuUtilizationPct).HasColumnName("cpu_utilization_pct");
        builder.Property(hs => hs.RamUtilizationPct).HasColumnName("ram_utilization_pct");
        builder.Property(hs => hs.NetRxKbps).HasColumnName("net_rx_kbps");
        builder.Property(hs => hs.NetTxKbps).HasColumnName("net_tx_kbps");
        builder.Property(hs => hs.TemperatureCelsius).HasColumnName("temperature_celsius");

        builder.HasOne(hs => hs.Device)
            .WithMany(d => d.HealthSamples)
            .HasForeignKey(hs => hs.DeviceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_health_device");

        builder.HasOne(hs => hs.StatusType)
            .WithMany(st => st.Samples)
            .HasForeignKey(hs => hs.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_status");

        builder.HasIndex(hs => new { hs.DeviceId, hs.RecordedAt }).HasDatabaseName("idx_health_device_time");
        builder.HasIndex(hs => hs.RecordedAt).HasDatabaseName("idx_health_time");

        // Disk volume samples are linked via sample_id across range-partitioned boundary
        builder.Ignore(hs => hs.DiskVolumeSamples);
    }
}

public class DiskVolumeSampleConfiguration : IEntityTypeConfiguration<DiskVolumeSample>
{
    public void Configure(EntityTypeBuilder<DiskVolumeSample> builder)
    {
        builder.ToTable("disk_volume_samples");

        builder.HasKey(dvs => dvs.VolumeSampleId).HasName("disk_volume_samples_pkey");
        builder.Property(dvs => dvs.VolumeSampleId).HasColumnName("volume_sample_id").ValueGeneratedOnAdd();
        builder.Property(dvs => dvs.SampleId).HasColumnName("sample_id").IsRequired();
        builder.Property(dvs => dvs.VolumeName).HasColumnName("volume_name").HasMaxLength(50).IsRequired();
        builder.Property(dvs => dvs.UsedPercentage).HasColumnName("used_percentage").IsRequired();
        builder.Property(dvs => dvs.TotalCapacityGb).HasColumnName("total_capacity_gb").IsRequired();
        builder.Property(dvs => dvs.FreeSpaceGb).HasColumnName("free_space_gb").IsRequired();

        builder.HasIndex(dvs => dvs.SampleId).HasDatabaseName("idx_volume_sample");
        builder.Ignore(dvs => dvs.HealthSample);
    }
}
