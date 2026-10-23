using System.Text;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class IngestServiceTests
{
    private readonly StubIngestRepository _ingestRepo;
    private readonly StubDeviceRepository _deviceRepo;
    private readonly StubGroupRepository _groupRepo;
    private readonly StubAuditLogRepository _auditRepo;
    private readonly StubMacVendorLookup _vendorLookup;
    private readonly IngestService _ingestService;

    public IngestServiceTests()
    {
        _deviceRepo = new StubDeviceRepository();
        _ingestRepo = new StubIngestRepository(_deviceRepo);
        _groupRepo = new StubGroupRepository();
        _auditRepo = new StubAuditLogRepository();
        _vendorLookup = new StubMacVendorLookup();

        _groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group" });
        _groupRepo.AddGroup(new Group { GroupId = 2, GroupName = "Computer Science Dept" });
        _groupRepo.AddGroup(new Group { GroupId = 3, GroupName = "CS Lab 101" });

        // Existing Core Router and Lab Switch in production
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 1,
            DisplayName = "CAMPUS-CORE-ROUTER",
            PrimaryIp = "10.0.0.1",
            MacAddress = "00:1A:2B:00:00:01",
            TypeId = 1,
            GroupId = 1
        });
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 2,
            DisplayName = "CS-LAB101-SW-01",
            PrimaryIp = "192.168.1.2",
            MacAddress = "00:1A:2B:00:00:02",
            TypeId = 3,
            GroupId = 3
        });

        _ingestService = new IngestService(
            _ingestRepo,
            _deviceRepo,
            _groupRepo,
            _auditRepo,
            _vendorLookup
        );
    }

    [Fact]
    public async Task UploadCsvStreamAsync_WhenValidCsv_ParsesAndStagesRecords_WithPreResolution()
    {
        var csvContent = @"mac_address,primary_ip,display_name,device_type,group_path,parent_identifier,zone,vendor,model,is_monitored
00:14:22:11:22:01,192.168.1.101,CS-LAB101-PC-01,PC,CS Lab 101,192.168.1.2,Desk 01,Dell,OptiPlex 7090,true
00:14:22:11:22:02,192.168.1.102,CS-LAB101-PC-02,PC,CS Lab 101,CS-LAB101-SW-01,Desk 02,Dell,OptiPlex 7090,true";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var response = await _ingestService.UploadCsvStreamAsync(stream, "lab_pcs.csv", userId: 1);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(1, response.BatchId);
        Assert.Equal("lab_pcs.csv", response.OriginalFilename);
        Assert.Equal("CSV", response.SourceFileType);
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.ValidRowsCount);
        Assert.Equal(0, response.IncompleteRowsCount);
        Assert.Equal(0, response.ConflictRowsCount);
        Assert.Equal("STAGED", response.Status);

        // Verify staged devices
        var (staged, total) = await _ingestRepo.GetStagedRecordsPagedAsync(response.BatchId, null, 1, 10);
        Assert.Equal(2, total);
        Assert.Equal(2, staged.Count);

        var first = staged[0];
        Assert.Equal("00:14:22:11:22:01", first.RawMacAddress);
        Assert.Equal("192.168.1.101", first.RawPrimaryIp);
        Assert.Equal("CS-LAB101-PC-01", first.RawDisplayName);
        Assert.Equal((short)5, first.ResolvedTypeId);           // PC
        Assert.Equal(3, first.ResolvedGroupId);          // CS Lab 101
        Assert.Equal(2, first.ResolvedParentDeviceId);   // Resolved parent switch ID 2
        Assert.Equal("VALID", first.ValidationStatus);
        Assert.Null(first.ValidationErrors);
    }

    [Fact]
    public async Task UploadCsvStreamAsync_WhenMacMissingOrInvalid_MarksRowIncomplete()
    {
        var csvContent = @"mac_address,primary_ip,display_name,device_type
,192.168.1.105,CS-LAB101-PC-05,PC
invalid-mac-string,192.168.1.106,CS-LAB101-PC-06,PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var response = await _ingestService.UploadCsvStreamAsync(stream, "incomplete_pcs.csv", userId: 1);

        // Assert
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(0, response.ValidRowsCount);
        Assert.Equal(2, response.IncompleteRowsCount);
        Assert.Equal(0, response.ConflictRowsCount);

        var (staged, _) = await _ingestRepo.GetStagedRecordsPagedAsync(response.BatchId, "INCOMPLETE", 1, 10);
        Assert.Equal(2, staged.Count);
        Assert.Contains("Missing required MAC Address", staged[0].ValidationErrors);
        Assert.Contains("Invalid MAC address format", staged[1].ValidationErrors);
    }

    [Fact]
    public async Task UploadCsvStreamAsync_WhenMacOrIpAlreadyInProduction_MarksRowConflict()
    {
        // 00:1A:2B:00:00:01 and 10.0.0.1 already exist in production
        var csvContent = @"mac_address,primary_ip,display_name
00:1A:2B:00:00:01,192.168.1.200,CONFLICT-MAC-PC
00:14:22:99:88:77,10.0.0.1,CONFLICT-IP-PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var response = await _ingestService.UploadCsvStreamAsync(stream, "conflicts.csv", userId: 1);

        // Assert
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(0, response.ValidRowsCount);
        Assert.Equal(2, response.ConflictRowsCount);

        var (staged, _) = await _ingestRepo.GetStagedRecordsPagedAsync(response.BatchId, "CONFLICT", 1, 10);
        Assert.Equal(2, staged.Count);
        Assert.Contains("MAC address conflicts with existing device", staged[0].ValidationErrors);
        Assert.Contains("IP address conflicts with existing device", staged[1].ValidationErrors);
    }

    [Fact]
    public async Task UploadCsvStreamAsync_WhenDuplicateMacInSameBatch_MarksRowConflict()
    {
        var csvContent = @"mac_address,primary_ip,display_name
00:14:22:77:88:99,192.168.1.210,DUPLICATE-1
00:14:22:77:88:99,192.168.1.211,DUPLICATE-2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var response = await _ingestService.UploadCsvStreamAsync(stream, "dupes.csv", userId: 1);

        // Assert
        Assert.Equal(2, response.TotalRows);
        Assert.Equal(1, response.ValidRowsCount);
        Assert.Equal(1, response.ConflictRowsCount);

        var (conflicts, _) = await _ingestRepo.GetStagedRecordsPagedAsync(response.BatchId, "CONFLICT", 1, 10);
        Assert.Single(conflicts);
        Assert.Contains("Duplicate MAC address detected within import batch", conflicts[0].ValidationErrors);
    }

    [Fact]
    public async Task UploadCsvStreamAsync_WhenNonCsvExtension_ThrowsInvalidFileException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample content"));

        await Assert.ThrowsAsync<InvalidFileException>(() =>
            _ingestService.UploadCsvStreamAsync(stream, "inventory.xlsx", userId: 1));
    }

    [Fact]
    public async Task UpdateStagedRecordAsync_WhenEditingIncompleteRowWithValidMac_ReEvaluatesToValid()
    {
        var csvContent = @"mac_address,primary_ip,display_name,device_type
,192.168.1.150,CS-LAB101-PC-50,PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var batch = await _ingestService.UploadCsvStreamAsync(stream, "single_incomplete.csv", userId: 1);

        var (records, _) = await _ingestRepo.GetStagedRecordsPagedAsync(batch.BatchId, null, 1, 10);
        var incompleteRow = records[0];
        Assert.Equal("INCOMPLETE", incompleteRow.ValidationStatus);

        // Act - Operator supplies valid MAC address in staging grid
        var updateRequest = new UpdateStagedRecordRequest(RawMacAddress: "00:14:22:AA:BB:CC");
        var result = await _ingestService.UpdateStagedRecordAsync(incompleteRow.StagedId, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("00:14:22:AA:BB:CC", result.RawMacAddress);
        Assert.Equal("VALID", result.ValidationStatus);
        Assert.Null(result.ValidationErrors);
        Assert.True(result.IsUserModified);
    }

    [Fact]
    public async Task CommitBatchAsync_WhenCommitValidOnlyTrue_CommitsValidRowsAndPreservesIncomplete()
    {
        var csvContent = @"mac_address,primary_ip,display_name,device_type
00:14:22:33:44:01,192.168.1.181,CS-LAB101-PC-81,PC
,192.168.1.182,CS-LAB101-PC-82,PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var batch = await _ingestService.UploadCsvStreamAsync(stream, "partial.csv", userId: 1);

        // Act - Network Admin commits valid rows only
        var commitResponse = await _ingestService.CommitBatchAsync(
            batch.BatchId,
            new CommitBatchRequest(CommitValidOnly: true),
            userId: 1,
            ipAddress: "127.0.0.1"
        );

        // Assert
        Assert.NotNull(commitResponse);
        Assert.Equal(batch.BatchId, commitResponse.BatchId);
        Assert.Equal("COMMITTED", commitResponse.Status);
        Assert.Equal(1, commitResponse.CommittedRowsCount);
        Assert.Equal(1, commitResponse.RemainingIncompleteCount);

        // Verify valid device is now present in production devices inventory
        var committedDevice = await _deviceRepo.GetByMacAsync("00:14:22:33:44:01");
        Assert.NotNull(committedDevice);
        Assert.Equal("CS-LAB101-PC-81", committedDevice.DisplayName);
        Assert.Equal("192.168.1.181", committedDevice.PrimaryIp);

        // Verify incomplete row remains staged
        var (remaining, totalRemaining) = await _ingestRepo.GetStagedRecordsPagedAsync(batch.BatchId, null, 1, 10);
        Assert.Equal(1, totalRemaining);
        Assert.Equal("INCOMPLETE", remaining[0].ValidationStatus);

        // Verify central audit log
        Assert.Equal(2, _auditRepo.Logs.Count);
        Assert.Equal("BATCH_UPLOAD", _auditRepo.Logs[0].ActionCode);
        var audit = _auditRepo.Logs[1];
        Assert.Equal("BATCH_COMMIT", audit.ActionCode);
        Assert.Equal("BATCH", audit.TargetEntity);
        Assert.Equal(batch.BatchId.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task CommitBatchAsync_WhenCommitValidOnlyFalseAndIncompletePresent_ThrowsBatchValidationConflictException()
    {
        var csvContent = @"mac_address,primary_ip,display_name,device_type
00:14:22:33:44:01,192.168.1.181,CS-LAB101-PC-81,PC
,192.168.1.182,CS-LAB101-PC-82,PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var batch = await _ingestService.UploadCsvStreamAsync(stream, "partial.csv", userId: 1);

        // Act & Assert - Attempting full commit without resolving incomplete row must be blocked
        var ex = await Assert.ThrowsAsync<BatchValidationConflictException>(() =>
            _ingestService.CommitBatchAsync(
                batch.BatchId,
                new CommitBatchRequest(CommitValidOnly: false),
                userId: 1,
                ipAddress: "127.0.0.1"
            ));

        Assert.Equal("BATCH_VALIDATION_CONFLICT", ex.ErrorCode);
    }

    [Fact]
    public async Task DiscardBatchAsync_PurgesStagingRecordsAndLogsAudit()
    {
        var csvContent = @"mac_address,primary_ip,display_name
00:14:22:55:66:77,192.168.1.199,DISCARD-PC";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var batch = await _ingestService.UploadCsvStreamAsync(stream, "to_discard.csv", userId: 1);

        // Act
        await _ingestService.DiscardBatchAsync(batch.BatchId, userId: 1, ipAddress: "127.0.0.1");

        // Assert
        var deletedBatch = await _ingestRepo.GetBatchByIdAsync(batch.BatchId);
        Assert.Null(deletedBatch);

        var audit = _auditRepo.Logs.FirstOrDefault(l => l.ActionCode == "BATCH_DISCARD");
        Assert.NotNull(audit);
        Assert.Equal("BATCH", audit.TargetEntity);
        Assert.Equal(batch.BatchId.ToString(), audit.TargetId);
    }
}

/// <summary>
/// In-memory stub of IIngestRepository for comprehensive unit testing.
/// </summary>
internal class StubIngestRepository : IIngestRepository
{
    private readonly StubDeviceRepository? _deviceRepo;
    private readonly List<ImportBatch> _batches = new();
    private readonly List<StagedDevice> _stagedDevices = new();
    private int _batchSequence = 0;
    private long _stagedSequence = 0;

    public StubIngestRepository(StubDeviceRepository? deviceRepo = null)
    {
        _deviceRepo = deviceRepo;
    }

    public Task<ImportBatch> CreateBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        batch.BatchId = ++_batchSequence;
        _batches.Add(batch);
        return Task.FromResult(batch);
    }

    public Task AddStagedDevicesBatchAsync(IEnumerable<StagedDevice> devices, CancellationToken ct = default)
    {
        foreach (var d in devices)
        {
            if (d.StagedId == 0)
            {
                d.StagedId = ++_stagedSequence;
            }
            _stagedDevices.Add(d);
        }
        return Task.CompletedTask;
    }

    public Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default)
    {
        return Task.FromResult(_batches.FirstOrDefault(b => b.BatchId == batchId));
    }

    public Task<IReadOnlyList<ImportBatch>> GetBatchesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ImportBatch>>(_batches.OrderByDescending(b => b.CreatedAt).ToList());
    }

    public Task<(IReadOnlyList<StagedDevice> Items, int TotalCount)> GetStagedRecordsPagedAsync(
        int batchId, string? status, int page, int limit, CancellationToken ct = default)
    {
        var query = _stagedDevices.Where(sd => sd.BatchId == batchId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(sd => string.Equals(sd.ValidationStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderBy(sd => sd.SourceRowNumber).ToList();
        var items = list.Skip((page - 1) * limit).Take(limit).ToList();
        return Task.FromResult<(IReadOnlyList<StagedDevice>, int)>((items, list.Count));
    }

    public Task<StagedDevice?> GetStagedRecordByIdAsync(long stagedId, CancellationToken ct = default)
    {
        return Task.FromResult(_stagedDevices.FirstOrDefault(sd => sd.StagedId == stagedId));
    }

    public Task UpdateStagedRecordAsync(StagedDevice record, CancellationToken ct = default)
    {
        var idx = _stagedDevices.FindIndex(sd => sd.StagedId == record.StagedId);
        if (idx >= 0)
        {
            _stagedDevices[idx] = record;
        }
        return Task.CompletedTask;
    }

    public Task UpdateBatchAsync(ImportBatch batch, CancellationToken ct = default)
    {
        var idx = _batches.FindIndex(b => b.BatchId == batch.BatchId);
        if (idx >= 0)
        {
            _batches[idx] = batch;
        }
        return Task.CompletedTask;
    }

    public Task DeleteBatchAsync(int batchId, CancellationToken ct = default)
    {
        _stagedDevices.RemoveAll(sd => sd.BatchId == batchId);
        _batches.RemoveAll(b => b.BatchId == batchId);
        return Task.CompletedTask;
    }

    public Task<(int CommittedCount, int RemainingIncompleteCount, DateTimeOffset CommittedAt)> CommitBatchAsync(
        int batchId, bool commitValidOnly, CancellationToken ct = default)
    {
        var batch = _batches.FirstOrDefault(b => b.BatchId == batchId);
        if (batch == null)
        {
            throw new BatchNotFoundException(batchId);
        }

        if (batch.Status is "COMMITTED" or "DISCARDED")
        {
            throw new BatchAlreadyCommittedException(batchId);
        }

        var stagedRows = _stagedDevices.Where(sd => sd.BatchId == batchId).ToList();
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

        var committedAt = DateTimeOffset.UtcNow;

        if (_deviceRepo != null)
        {
            foreach (var row in validRows)
            {
                var normalizedMac = row.RawMacAddress!.Trim().ToUpperInvariant();
                var dev = new Device
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
                    CreatedAt = committedAt,
                    UpdatedAt = committedAt
                };
                _deviceRepo.AddDevice(dev);
            }
        }

        _stagedDevices.RemoveAll(sd => sd.BatchId == batchId && sd.ValidationStatus == "VALID");

        batch.CommittedAt = committedAt;
        batch.Status = "COMMITTED";
        batch.TotalRows = nonValidRows.Count;

        return Task.FromResult((validRows.Count, nonValidRows.Count, committedAt));
    }
}
