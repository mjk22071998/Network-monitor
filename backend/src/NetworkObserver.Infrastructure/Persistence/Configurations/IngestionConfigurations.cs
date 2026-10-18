using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batches");

        builder.HasKey(ib => ib.BatchId).HasName("import_batches_pkey");
        builder.Property(ib => ib.BatchId).HasColumnName("batch_id").ValueGeneratedOnAdd();
        builder.Property(ib => ib.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(ib => ib.SourceFileType).HasColumnName("source_file_type").HasMaxLength(20).IsRequired();
        builder.Property(ib => ib.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255).IsRequired();
        builder.Property(ib => ib.TotalRows).HasColumnName("total_rows").HasDefaultValue(0).IsRequired();
        builder.Property(ib => ib.Status).HasColumnName("status").HasMaxLength(30).HasDefaultValue("STAGED").IsRequired();
        builder.Property(ib => ib.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(ib => ib.CommittedAt).HasColumnName("committed_at");

        builder.HasOne(ib => ib.UploadedByUser)
            .WithMany(u => u.ImportBatches)
            .HasForeignKey(ib => ib.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_batch_user");
    }
}

public class StagedDeviceConfiguration : IEntityTypeConfiguration<StagedDevice>
{
    public void Configure(EntityTypeBuilder<StagedDevice> builder)
    {
        builder.ToTable("staged_devices");

        builder.HasKey(sd => sd.StagedId).HasName("staged_devices_pkey");
        builder.Property(sd => sd.StagedId).HasColumnName("staged_id").ValueGeneratedOnAdd();
        builder.Property(sd => sd.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(sd => sd.SourceRowNumber).HasColumnName("source_row_number").IsRequired();
        builder.Property(sd => sd.RawMacAddress).HasColumnName("raw_mac_address").HasMaxLength(50);
        builder.Property(sd => sd.RawPrimaryIp).HasColumnName("raw_primary_ip").HasMaxLength(50);
        builder.Property(sd => sd.RawDisplayName).HasColumnName("raw_display_name").HasMaxLength(150);
        builder.Property(sd => sd.RawDeviceType).HasColumnName("raw_device_type").HasMaxLength(50);
        builder.Property(sd => sd.RawGroupPath).HasColumnName("raw_group_path").HasMaxLength(255);
        builder.Property(sd => sd.RawParentIdentifier).HasColumnName("raw_parent_identifier").HasMaxLength(50);
        builder.Property(sd => sd.RawZone).HasColumnName("raw_zone").HasMaxLength(100);
        builder.Property(sd => sd.RawVendor).HasColumnName("raw_vendor").HasMaxLength(100);
        builder.Property(sd => sd.RawModel).HasColumnName("raw_model").HasMaxLength(100);
        builder.Property(sd => sd.IsMonitored).HasColumnName("is_monitored").HasDefaultValue(true).IsRequired();
        builder.Property(sd => sd.ResolvedTypeId).HasColumnName("resolved_type_id");
        builder.Property(sd => sd.ResolvedGroupId).HasColumnName("resolved_group_id");
        builder.Property(sd => sd.ResolvedParentDeviceId).HasColumnName("resolved_parent_device_id");
        builder.Property(sd => sd.ValidationStatus).HasColumnName("validation_status").HasMaxLength(30).HasDefaultValue("INCOMPLETE").IsRequired();
        builder.Property(sd => sd.ValidationErrors).HasColumnName("validation_errors");
        builder.Property(sd => sd.IsUserModified).HasColumnName("is_user_modified").HasDefaultValue(false).IsRequired();

        builder.HasOne(sd => sd.Batch)
            .WithMany(ib => ib.StagedDevices)
            .HasForeignKey(sd => sd.BatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_staged_batch");

        builder.HasOne(sd => sd.ResolvedType)
            .WithMany()
            .HasForeignKey(sd => sd.ResolvedTypeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_staged_type");

        builder.HasOne(sd => sd.ResolvedGroup)
            .WithMany()
            .HasForeignKey(sd => sd.ResolvedGroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_staged_group");

        builder.HasOne(sd => sd.ResolvedParentDevice)
            .WithMany()
            .HasForeignKey(sd => sd.ResolvedParentDeviceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_staged_parent");

        builder.HasIndex(sd => new { sd.BatchId, sd.ValidationStatus }).HasDatabaseName("idx_staged_batch_status");
    }
}
