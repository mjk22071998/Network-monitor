using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 10 implementation of IIngestRepository.
/// Strictly 100% compile-time type-safe LINQ queries, zero raw SQL, and transactional ACID guarantees.
/// </summary>
public class IngestRepository : IIngestRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public IngestRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ImportBatch> CreateBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync(ct);
        return batch;
    }

    public async Task AddStagedDevicesBatchAsync(IEnumerable<StagedDevice> devices, CancellationToken ct = default)
    {
        // Batch in 500-row chunks with ChangeTracker.Clear() to enforce memory bounds
        const int chunkSize = 500;
        var chunk = new List<StagedDevice>(chunkSize);

        foreach (var device in devices)
        {
            chunk.Add(device);
            if (chunk.Count >= chunkSize)
            {
                _dbContext.StagedDevices.AddRange(chunk);
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            _dbContext.StagedDevices.AddRange(chunk);
            await _dbContext.SaveChangesAsync(ct);
            _dbContext.ChangeTracker.Clear();
            chunk.Clear();
        }
    }

    public async Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default)
    {
        return await _dbContext.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchId == batchId, ct);
    }

    public async Task<IReadOnlyList<ImportBatch>> GetBatchesAsync(CancellationToken ct = default)
    {
        return await _dbContext.ImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<StagedDevice> Items, int TotalCount)> GetStagedRecordsPagedAsync(
        int batchId, string? status, int page, int limit, CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeLimit = Math.Clamp(limit, 1, 100);

        var query = _dbContext.StagedDevices
            .AsNoTracking()
            .Where(sd => sd.BatchId == batchId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(sd => sd.ValidationStatus == status.ToUpperInvariant());
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(sd => sd.SourceRowNumber)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<StagedDevice?> GetStagedRecordByIdAsync(long stagedId, CancellationToken ct = default)
    {
        return await _dbContext.StagedDevices
            .FirstOrDefaultAsync(sd => sd.StagedId == stagedId, ct);
    }

    public async Task UpdateStagedRecordAsync(StagedDevice record, CancellationToken ct = default)
    {
        _dbContext.StagedDevices.Update(record);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        _dbContext.ImportBatches.Update(batch);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteBatchAsync(int batchId, CancellationToken ct = default)
    {
        var batch = await _dbContext.ImportBatches.FirstOrDefaultAsync(b => b.BatchId == batchId, ct);
        if (batch != null)
        {
            _dbContext.ImportBatches.Remove(batch);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<(int CommittedCount, int RemainingIncompleteCount, DateTimeOffset CommittedAt)> CommitBatchAsync(
        int batchId, bool commitValidOnly, CancellationToken ct = default)
    {
        var batch = await _dbContext.ImportBatches.FirstOrDefaultAsync(b => b.BatchId == batchId, ct);
        if (batch == null)
        {
            throw new BatchNotFoundException(batchId);
        }

        if (batch.Status is "COMMITTED" or "DISCARDED")
        {
            throw new BatchAlreadyCommittedException(batchId);
        }

        var stagedRows = await _dbContext.StagedDevices
            .Where(sd => sd.BatchId == batchId)
            .ToListAsync(ct);

        var validRows = stagedRows.Where(r => r.ValidationStatus == "VALID").ToList();
        var nonValidRows = stagedRows.Where(r => r.ValidationStatus != "VALID").ToList();

        if (!commitValidOnly && nonValidRows.Count > 0)
        {
            throw new BatchValidationConflictException(
                $"Batch contains {nonValidRows.Count} incomplete or conflicting records. Resolve conflicts or set commit_valid_only to true.");
        }

        if (validRows.Count == 0)
        {
            throw new BatchValidationConflictException("No valid records found in batch to commit.");
        }

        // ACID Transaction Guarantee with EF Core retrying execution strategy
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var committedDevices = new List<Device>(validRows.Count);

                foreach (var row in validRows)
                {
                    var normalizedMac = row.RawMacAddress!.Trim().ToUpperInvariant();

                    // Concurrency uniqueness check against production inventory
                    var exists = await _dbContext.Devices.AnyAsync(d => d.MacAddress == normalizedMac, ct);
                    if (exists)
                    {
                        throw new DuplicateMacException(normalizedMac);
                    }

                    var device = new Device
                    {
                        MacAddress = normalizedMac,
                        PrimaryIp = row.RawPrimaryIp!.Trim(),
                        DisplayName = row.RawDisplayName!.Trim(),
                        TypeId = row.ResolvedTypeId ?? 5,
                        GroupId = row.ResolvedGroupId ?? 1,
                        ParentDeviceId = row.ResolvedParentDeviceId,
                        Zone = row.RawZone,
                        Vendor = row.RawVendor,
                        Model = row.RawModel,
                        IsMonitored = row.IsMonitored,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    committedDevices.Add(device);
                }

                _dbContext.Devices.AddRange(committedDevices);
                await _dbContext.SaveChangesAsync(ct);

                // Purge committed rows from staging to leave only remaining incomplete/conflict rows
                _dbContext.StagedDevices.RemoveRange(validRows);

                var committedAt = DateTimeOffset.UtcNow;
                batch.CommittedAt = committedAt;
                batch.Status = "COMMITTED";
                batch.TotalRows = nonValidRows.Count;
                _dbContext.ImportBatches.Update(batch);

                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return (validRows.Count, nonValidRows.Count, committedAt);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
