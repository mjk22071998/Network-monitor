using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for Ingestion staging tables and atomic commit execution.
/// Complies with GoF Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IIngestRepository
{
    Task<ImportBatch> CreateBatchAsync(ImportBatch batch, CancellationToken ct = default);
    Task AddStagedDevicesBatchAsync(IEnumerable<StagedDevice> devices, CancellationToken ct = default);
    Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> GetBatchesAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<StagedDevice> Items, int TotalCount)> GetStagedRecordsPagedAsync(
        int batchId, string? status, int page, int limit, CancellationToken ct = default);
    Task<StagedDevice?> GetStagedRecordByIdAsync(long stagedId, CancellationToken ct = default);
    Task UpdateStagedRecordAsync(StagedDevice record, CancellationToken ct = default);
    Task UpdateBatchAsync(ImportBatch batch, CancellationToken ct = default);
    Task DeleteBatchAsync(int batchId, CancellationToken ct = default);
    Task<(int CommittedCount, int RemainingIncompleteCount, DateTimeOffset CommittedAt)> CommitBatchAsync(
        int batchId, bool commitValidOnly, CancellationToken ct = default);
}
