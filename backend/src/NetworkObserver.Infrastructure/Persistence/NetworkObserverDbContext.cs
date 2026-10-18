using Microsoft.EntityFrameworkCore;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence;

public class NetworkObserverDbContext : DbContext
{
    public NetworkObserverDbContext(DbContextOptions<NetworkObserverDbContext> options)
        : base(options)
    {
    }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<DeviceType> DeviceTypes => Set<DeviceType>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<MonitoringConfig> MonitoringConfigs => Set<MonitoringConfig>();
    public DbSet<ReachabilityStatusType> ReachabilityStatusTypes => Set<ReachabilityStatusType>();
    public DbSet<ReachabilitySample> ReachabilitySamples => Set<ReachabilitySample>();
    public DbSet<HealthStatusType> HealthStatusTypes => Set<HealthStatusType>();
    public DbSet<HealthSample> HealthSamples => Set<HealthSample>();
    public DbSet<DiskVolumeSample> DiskVolumeSamples => Set<DiskVolumeSample>();
    public DbSet<PhysicalLayout> PhysicalLayouts => Set<PhysicalLayout>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<StagedDevice> StagedDevices => Set<StagedDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NetworkObserverDbContext).Assembly);
    }
}
