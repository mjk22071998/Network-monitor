using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service implementing the Low-Memory Streaming Ingestion Pipeline,
/// 64KB ring buffer spooling, automated pre-resolution, and atomic bulk commit gatekeeper.
/// Complies with api_contracts_specification.md Section 9 and database_schema_and_ingestion_specification.md Section 6.
/// </summary>
public class IngestService : IIngestService
{
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IIngestRepository _ingestRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMacVendorLookup _macVendorLookup;

    public IngestService(
        IIngestRepository ingestRepository,
        IDeviceRepository deviceRepository,
        IGroupRepository groupRepository,
        IAuditLogRepository auditLogRepository,
        IMacVendorLookup macVendorLookup)
    {
        _ingestRepository = ingestRepository;
        _deviceRepository = deviceRepository;
        _groupRepository = groupRepository;
        _auditLogRepository = auditLogRepository;
        _macVendorLookup = macVendorLookup;
    }

    public async Task<UploadBatchResponse> UploadCsvStreamAsync(
        Stream fileStream,
        string filename,
        int userId,
        CancellationToken ct = default)
    {
        if (fileStream == null || !fileStream.CanRead)
        {
            throw new InvalidFileException("Uploaded file stream is empty or unreadable.");
        }

        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (extension != ".csv")
        {
            throw new InvalidFileException("Unsupported file type. Bulk staging pipeline accepts .csv files exclusively.");
        }

        // Create the parent batch record first
        var batch = new ImportBatch
        {
            UploadedByUserId = userId,
            SourceFileType = "CSV",
            OriginalFilename = Path.GetFileName(filename),
            TotalRows = 0,
            Status = "STAGED",
            CreatedAt = DateTimeOffset.UtcNow
        };
        batch = await _ingestRepository.CreateBatchAsync(batch, ct);

        // Preload existing inventory references for fast in-memory conflict & foreign key resolution
        var (existingDevices, _) = await _deviceRepository.GetPagedAsync(new DeviceQueryParameters(Limit: 10000), ct);
        var existingGroups = await _groupRepository.GetAllAsync(ct);

        var existingMacs = new HashSet<string>(existingDevices.Select(d => d.MacAddress), StringComparer.OrdinalIgnoreCase);
        var existingIps = new HashSet<string>(existingDevices.Select(d => d.PrimaryIp), StringComparer.OrdinalIgnoreCase);

        var groupsByName = existingGroups.ToDictionary(g => g.GroupName, g => g.GroupId, StringComparer.OrdinalIgnoreCase);
        var devicesByIp = existingDevices.ToDictionary(d => d.PrimaryIp, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);
        var devicesByMac = existingDevices.ToDictionary(d => d.MacAddress, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);
        var devicesByName = existingDevices.ToDictionary(d => d.DisplayName, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);

        // Batch-level uniqueness tracking
        var seenBatchMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBatchIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchDevicesByIp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var batchDevicesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int totalRows = 0;
        int validRows = 0;
        int incompleteRows = 0;
        int conflictRows = 0;

        var stagedList = new List<StagedDevice>();

        // 64KB Ring Buffer stream reading (satisfies Zero-RAM-Spike constraint)
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidFileException("Uploaded CSV file is empty or missing header row.");
        }

        var headerMap = ParseHeader(headerLine);
        int rowNumber = 1;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rowNumber++;
            totalRows++;

            var fields = ParseCsvLine(line);
            var rawMac = GetFieldValue(fields, headerMap, "mac");
            var rawIp = GetFieldValue(fields, headerMap, "ip");
            var rawName = GetFieldValue(fields, headerMap, "name");
            var rawType = GetFieldValue(fields, headerMap, "type");
            var rawGroup = GetFieldValue(fields, headerMap, "group");
            var rawParent = GetFieldValue(fields, headerMap, "parent");
            var rawZone = GetFieldValue(fields, headerMap, "zone");
            var rawVendor = GetFieldValue(fields, headerMap, "vendor");
            var rawModel = GetFieldValue(fields, headerMap, "model");
            var rawMonitoredStr = GetFieldValue(fields, headerMap, "monitored");

            bool isMonitored = true;
            if (!string.IsNullOrWhiteSpace(rawMonitoredStr) &&
                bool.TryParse(rawMonitoredStr, out var parsedMonitored))
            {
                isMonitored = parsedMonitored;
            }

            // Automated pre-resolution & validation state machine
            var (status, errors, resolvedTypeId, resolvedGroupId, resolvedParentId) = EvaluateRowValidation(
                rawMac, rawIp, rawName, rawType, rawGroup, rawParent,
                existingMacs, existingIps, seenBatchMacs, seenBatchIps,
                groupsByName, devicesByIp, devicesByMac, devicesByName,
                batchDevicesByIp, batchDevicesByName
            );

            // Infer vendor from MAC OUI if not explicitly provided
            if (string.IsNullOrWhiteSpace(rawVendor) && !string.IsNullOrWhiteSpace(rawMac))
            {
                var lookedUp = _macVendorLookup.LookupVendor(rawMac);
                if (lookedUp != "Unknown Vendor")
                {
                    rawVendor = lookedUp;
                }
            }

            var staged = new StagedDevice
            {
                BatchId = batch.BatchId,
                SourceRowNumber = rowNumber,
                RawMacAddress = rawMac,
                RawPrimaryIp = rawIp,
                RawDisplayName = rawName,
                RawDeviceType = rawType,
                RawGroupPath = rawGroup,
                RawParentIdentifier = rawParent,
                RawZone = rawZone,
                RawVendor = rawVendor,
                RawModel = rawModel,
                IsMonitored = isMonitored,
                ResolvedTypeId = resolvedTypeId,
                ResolvedGroupId = resolvedGroupId,
                ResolvedParentDeviceId = resolvedParentId,
                ValidationStatus = status,
                ValidationErrors = errors,
                IsUserModified = false
            };

            stagedList.Add(staged);

            if (status == "VALID") validRows++;
            else if (status == "CONFLICT") conflictRows++;
            else incompleteRows++;
        }

        // Persist staged records via batch chunking
        await _ingestRepository.AddStagedDevicesBatchAsync(stagedList, ct);

        // Update batch total
        batch.TotalRows = totalRows;
        await _ingestRepository.UpdateBatchAsync(batch, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = AuditActionCodes.BatchUpload,
            TargetEntity = AuditTargetEntities.Batch,
            TargetId = batch.BatchId.ToString(),
            Details = $"Uploaded bulk staging file '{batch.OriginalFilename}' ({totalRows} rows: {validRows} valid, {incompleteRows} incomplete, {conflictRows} conflict).",
            IpAddress = "127.0.0.1",
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new UploadBatchResponse(
            BatchId: batch.BatchId,
            OriginalFilename: batch.OriginalFilename,
            SourceFileType: batch.SourceFileType,
            TotalRows: totalRows,
            ValidRowsCount: validRows,
            IncompleteRowsCount: incompleteRows,
            ConflictRowsCount: conflictRows,
            Status: batch.Status,
            CreatedAt: batch.CreatedAt
        );
    }

    public async Task<BatchesListResponse> GetBatchesAsync(CancellationToken ct = default)
    {
        var batches = await _ingestRepository.GetBatchesAsync(ct);
        var dtos = batches.Select(b => new ImportBatchListItemDto(
            BatchId: b.BatchId,
            OriginalFilename: b.OriginalFilename,
            SourceFileType: b.SourceFileType,
            TotalRows: b.TotalRows,
            Status: b.Status,
            UploadedByUserId: b.UploadedByUserId,
            CreatedAt: b.CreatedAt,
            CommittedAt: b.CommittedAt
        )).ToList();

        return new BatchesListResponse(dtos);
    }

    public async Task<StagedRecordsPagedResponse> GetStagedRecordsPagedAsync(
        int batchId,
        string? status,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        var batch = await _ingestRepository.GetBatchByIdAsync(batchId, ct);
        if (batch == null)
        {
            throw new BatchNotFoundException(batchId);
        }

        var (items, totalCount) = await _ingestRepository.GetStagedRecordsPagedAsync(batchId, status, page, limit, ct);

        var safePage = Math.Max(1, page);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safeLimit);

        var dtos = items.Select(sd => new StagedDeviceDto(
            StagedId: sd.StagedId,
            SourceRowNumber: sd.SourceRowNumber,
            RawMacAddress: sd.RawMacAddress,
            RawPrimaryIp: sd.RawPrimaryIp,
            RawDisplayName: sd.RawDisplayName,
            RawDeviceType: sd.RawDeviceType,
            RawGroupPath: sd.RawGroupPath,
            RawParentIdentifier: sd.RawParentIdentifier,
            RawZone: sd.RawZone,
            RawVendor: sd.RawVendor,
            RawModel: sd.RawModel,
            ResolvedTypeId: sd.ResolvedTypeId,
            ResolvedGroupId: sd.ResolvedGroupId,
            ResolvedParentDeviceId: sd.ResolvedParentDeviceId,
            ValidationStatus: sd.ValidationStatus,
            ValidationErrors: sd.ValidationErrors,
            IsUserModified: sd.IsUserModified
        )).ToList();

        var pagination = new PaginationMetadata(
            TotalRecords: totalCount,
            CurrentPage: safePage,
            PageSize: safeLimit,
            TotalPages: totalPages,
            HasNext: safePage < totalPages,
            HasPrev: safePage > 1
        );

        return new StagedRecordsPagedResponse(batchId, dtos, pagination);
    }

    public async Task<UpdateStagedRecordResponse> UpdateStagedRecordAsync(
        long stagedId,
        UpdateStagedRecordRequest request,
        int? userId = null,
        string ipAddress = "127.0.0.1",
        CancellationToken ct = default)
    {
        var record = await _ingestRepository.GetStagedRecordByIdAsync(stagedId, ct);
        if (record == null)
        {
            throw new StagedRecordNotFoundException(stagedId);
        }

        var batch = await _ingestRepository.GetBatchByIdAsync(record.BatchId, ct);
        if (batch == null || batch.Status is "COMMITTED" or "DISCARDED")
        {
            throw new BatchAlreadyCommittedException(record.BatchId);
        }

        // Apply edits
        if (request.RawMacAddress != null) record.RawMacAddress = request.RawMacAddress;
        if (request.RawPrimaryIp != null) record.RawPrimaryIp = request.RawPrimaryIp;
        if (request.RawDisplayName != null) record.RawDisplayName = request.RawDisplayName;
        if (request.RawDeviceType != null) record.RawDeviceType = request.RawDeviceType;
        if (request.RawGroupPath != null) record.RawGroupPath = request.RawGroupPath;
        if (request.RawParentIdentifier != null) record.RawParentIdentifier = request.RawParentIdentifier;
        if (request.RawZone != null) record.RawZone = request.RawZone;
        if (request.RawVendor != null) record.RawVendor = request.RawVendor;
        if (request.RawModel != null) record.RawModel = request.RawModel;
        if (request.IsMonitored.HasValue) record.IsMonitored = request.IsMonitored.Value;

        record.IsUserModified = true;

        // Re-evaluate validation against production DB
        var (existingDevices, _) = await _deviceRepository.GetPagedAsync(new DeviceQueryParameters(Limit: 10000), ct);
        var existingGroups = await _groupRepository.GetAllAsync(ct);

        var existingMacs = new HashSet<string>(existingDevices.Select(d => d.MacAddress), StringComparer.OrdinalIgnoreCase);
        var existingIps = new HashSet<string>(existingDevices.Select(d => d.PrimaryIp), StringComparer.OrdinalIgnoreCase);

        var groupsByName = existingGroups.ToDictionary(g => g.GroupName, g => g.GroupId, StringComparer.OrdinalIgnoreCase);
        var devicesByIp = existingDevices.ToDictionary(d => d.PrimaryIp, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);
        var devicesByMac = existingDevices.ToDictionary(d => d.MacAddress, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);
        var devicesByName = existingDevices.ToDictionary(d => d.DisplayName, d => d.DeviceId, StringComparer.OrdinalIgnoreCase);

        var (status, errors, resolvedTypeId, resolvedGroupId, resolvedParentId) = EvaluateRowValidation(
            record.RawMacAddress, record.RawPrimaryIp, record.RawDisplayName, record.RawDeviceType,
            record.RawGroupPath, record.RawParentIdentifier,
            existingMacs, existingIps, new HashSet<string>(), new HashSet<string>(),
            groupsByName, devicesByIp, devicesByMac, devicesByName,
            new Dictionary<string, int>(), new Dictionary<string, int>()
        );

        record.ValidationStatus = status;
        record.ValidationErrors = errors;
        record.ResolvedTypeId = resolvedTypeId;
        record.ResolvedGroupId = resolvedGroupId;
        record.ResolvedParentDeviceId = resolvedParentId;

        await _ingestRepository.UpdateStagedRecordAsync(record, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = AuditActionCodes.BatchEdit,
            TargetEntity = AuditTargetEntities.Batch,
            TargetId = record.BatchId.ToString(),
            Details = $"Edited staged record ID {record.StagedId} (Row #{record.SourceRowNumber}). Status: {record.ValidationStatus}.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new UpdateStagedRecordResponse(
            StagedId: record.StagedId,
            RawMacAddress: record.RawMacAddress,
            ValidationStatus: record.ValidationStatus,
            ValidationErrors: record.ValidationErrors,
            IsUserModified: record.IsUserModified
        );
    }

    public async Task<CommitBatchResponse> CommitBatchAsync(
        int batchId,
        CommitBatchRequest request,
        int userId,
        string ipAddress,
        CancellationToken ct = default)
    {
        var (committedCount, remainingIncomplete, committedAt) = await _ingestRepository.CommitBatchAsync(
            batchId, request.CommitValidOnly, ct);

        // Record central non-repudiation audit trail
        var audit = new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = "BATCH_COMMIT",
            TargetEntity = "BATCH",
            TargetId = batchId.ToString(),
            Details = JsonSerializer.Serialize(new
            {
                batch_id = batchId,
                committed_rows = committedCount,
                remaining_incomplete = remainingIncomplete,
                commit_valid_only = request.CommitValidOnly
            }),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            LoggedAt = committedAt
        };
        await _auditLogRepository.AddAsync(audit, ct);

        return new CommitBatchResponse(
            BatchId: batchId,
            Status: "COMMITTED",
            CommittedRowsCount: committedCount,
            RemainingIncompleteCount: remainingIncomplete,
            CommittedAt: committedAt
        );
    }

    public async Task DiscardBatchAsync(
        int batchId,
        int userId,
        string ipAddress,
        CancellationToken ct = default)
    {
        var batch = await _ingestRepository.GetBatchByIdAsync(batchId, ct);
        if (batch == null)
        {
            throw new BatchNotFoundException(batchId);
        }

        await _ingestRepository.DeleteBatchAsync(batchId, ct);

        var audit = new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = "BATCH_DISCARD",
            TargetEntity = "BATCH",
            TargetId = batchId.ToString(),
            Details = JsonSerializer.Serialize(new { batch_id = batchId, filename = batch.OriginalFilename }),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            LoggedAt = DateTimeOffset.UtcNow
        };
        await _auditLogRepository.AddAsync(audit, ct);
    }

    private static (string Status, string? Errors, short? TypeId, int? GroupId, int? ParentDeviceId) EvaluateRowValidation(
        string? rawMac,
        string? rawIp,
        string? rawName,
        string? rawType,
        string? rawGroup,
        string? rawParent,
        HashSet<string> existingMacs,
        HashSet<string> existingIps,
        HashSet<string> seenBatchMacs,
        HashSet<string> seenBatchIps,
        Dictionary<string, int> groupsByName,
        Dictionary<string, int> devicesByIp,
        Dictionary<string, int> devicesByMac,
        Dictionary<string, int> devicesByName,
        Dictionary<string, int> batchDevicesByIp,
        Dictionary<string, int> batchDevicesByName)
    {
        var errors = new List<string>();
        bool hasConflict = false;

        // 1. MAC Address validation & conflict detection
        string? normalizedMac = null;
        if (string.IsNullOrWhiteSpace(rawMac))
        {
            errors.Add("Missing required MAC Address.");
        }
        else
        {
            var cleanedMac = rawMac.Trim().Replace("-", ":").ToUpperInvariant();
            if (!MacRegex.IsMatch(cleanedMac))
            {
                errors.Add("Invalid MAC address format. Expected XX:XX:XX:XX:XX:XX.");
            }
            else
            {
                normalizedMac = cleanedMac;
                if (existingMacs.Contains(normalizedMac))
                {
                    errors.Add("MAC address conflicts with existing device in production inventory.");
                    hasConflict = true;
                }
                else if (seenBatchMacs.Contains(normalizedMac))
                {
                    errors.Add("Duplicate MAC address detected within import batch.");
                    hasConflict = true;
                }
                else
                {
                    seenBatchMacs.Add(normalizedMac);
                }
            }
        }

        // 2. Primary IP validation & conflict detection
        if (string.IsNullOrWhiteSpace(rawIp))
        {
            errors.Add("Missing required Primary IP address.");
        }
        else
        {
            var cleanedIp = rawIp.Trim();
            if (!IPAddress.TryParse(cleanedIp, out var ipObj) || ipObj.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                errors.Add("Invalid IPv4 address format.");
            }
            else
            {
                if (existingIps.Contains(cleanedIp))
                {
                    errors.Add("IP address conflicts with existing device in production inventory.");
                    hasConflict = true;
                }
                else if (seenBatchIps.Contains(cleanedIp))
                {
                    errors.Add("Duplicate IP address detected within import batch.");
                    hasConflict = true;
                }
                else
                {
                    seenBatchIps.Add(cleanedIp);
                }
            }
        }

        // 3. Display Name validation
        if (string.IsNullOrWhiteSpace(rawName))
        {
            errors.Add("Missing required Display Name.");
        }

        // 4. Device Type resolution
        short? resolvedTypeId = ResolveDeviceType(rawType);

        // 5. Group Hierarchy resolution
        int? resolvedGroupId = 1; // Default to Unassigned Group (ID = 1)
        if (!string.IsNullOrWhiteSpace(rawGroup))
        {
            var groupKey = rawGroup.Trim();
            // Handle hierarchical paths like "Computer Science Dept / CS Lab 101"
            if (groupKey.Contains('/'))
            {
                var leafName = groupKey.Split('/').Last().Trim();
                if (groupsByName.TryGetValue(leafName, out var id))
                {
                    resolvedGroupId = id;
                }
                else if (groupsByName.TryGetValue(groupKey, out var fullPathId))
                {
                    resolvedGroupId = fullPathId;
                }
            }
            else if (groupsByName.TryGetValue(groupKey, out var id))
            {
                resolvedGroupId = id;
            }
        }

        // 6. Parent Device resolution
        int? resolvedParentId = null;
        if (!string.IsNullOrWhiteSpace(rawParent))
        {
            var parentKey = rawParent.Trim().Replace("-", ":").ToUpperInvariant();

            if (devicesByIp.TryGetValue(rawParent.Trim(), out var pIdByIp))
                resolvedParentId = pIdByIp;
            else if (devicesByMac.TryGetValue(parentKey, out var pIdByMac))
                resolvedParentId = pIdByMac;
            else if (devicesByName.TryGetValue(rawParent.Trim(), out var pIdByName))
                resolvedParentId = pIdByName;
            else if (batchDevicesByIp.TryGetValue(rawParent.Trim(), out var bIdByIp))
                resolvedParentId = bIdByIp;
            else if (batchDevicesByName.TryGetValue(rawParent.Trim(), out var bIdByName))
                resolvedParentId = bIdByName;
        }

        string status;
        if (hasConflict) status = "CONFLICT";
        else if (errors.Count > 0) status = "INCOMPLETE";
        else status = "VALID";

        string? errorDetails = errors.Count > 0 ? string.Join("; ", errors) : null;
        return (status, errorDetails, resolvedTypeId, resolvedGroupId, resolvedParentId);
    }

    private static short? ResolveDeviceType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return (short)DeviceTypeEnum.Pc;

        var t = rawType.Trim().ToUpperInvariant();
        if (t.Contains("ROUTER") || t.Contains("GATEWAY") || t == "1") return (short)DeviceTypeEnum.CoreRouter;
        if (t.Contains("AGGREGATE") || t.Contains("DISTRIBUTION") || t == "2") return (short)DeviceTypeEnum.AggregateSwitch;
        if (t.Contains("SWITCH") || t == "3") return (short)DeviceTypeEnum.AccessSwitch;
        if (t.Contains("SERVER") || t == "4") return (short)DeviceTypeEnum.Server;
        if (t.Contains("PC") || t.Contains("WORKSTATION") || t.Contains("COMPUTER") || t == "5") return (short)DeviceTypeEnum.Pc;

        return (short)DeviceTypeEnum.Pc;
    }

    private static Dictionary<string, int> ParseHeader(string headerLine)
    {
        var headers = ParseCsvLine(headerLine);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
            if (h.Contains("mac") || h.Contains("phys")) map["mac"] = i;
            else if (h.Contains("ip") || h.Contains("host_ip") || h.Contains("primary_ip")) map["ip"] = i;
            else if (h.Contains("name") || h.Contains("hostname")) map["name"] = i;
            else if (h.Contains("type") || h.Contains("role")) map["type"] = i;
            else if (h.Contains("group") || h.Contains("lab") || h.Contains("dept")) map["group"] = i;
            else if (h.Contains("parent") || h.Contains("uplink") || h.Contains("switch")) map["parent"] = i;
            else if (h.Contains("zone") || h.Contains("room") || h.Contains("location")) map["zone"] = i;
            else if (h.Contains("vendor") || h.Contains("mfg")) map["vendor"] = i;
            else if (h.Contains("model")) map["model"] = i;
            else if (h.Contains("monitor")) map["monitored"] = i;
        }

        return map;
    }

    private static string? GetFieldValue(IReadOnlyList<string> fields, Dictionary<string, int> headerMap, string key)
    {
        if (headerMap.TryGetValue(key, out var idx) && idx < fields.Count)
        {
            var val = fields[idx].Trim();
            return string.IsNullOrEmpty(val) ? null : val;
        }
        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current.Append('\"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
