namespace NetworkObserver.Application.DTOs;

/// <summary>
/// DTO contracts for Bulk Ingestion Pipeline complying with
/// api_contracts_specification.md Section 9.
/// </summary>
public record UploadBatchResponse(
    int BatchId,
    string OriginalFilename,
    string SourceFileType,
    int TotalRows,
    int ValidRowsCount,
    int IncompleteRowsCount,
    int ConflictRowsCount,
    string Status,
    DateTimeOffset CreatedAt
);

public record ImportBatchListItemDto(
    int BatchId,
    string OriginalFilename,
    string SourceFileType,
    int TotalRows,
    string Status,
    int UploadedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt
);

public record BatchesListResponse(
    IReadOnlyList<ImportBatchListItemDto> Batches
);

public record StagedDeviceDto(
    long StagedId,
    int SourceRowNumber,
    string? RawMacAddress,
    string? RawPrimaryIp,
    string? RawDisplayName,
    string? RawDeviceType,
    string? RawGroupPath,
    string? RawParentIdentifier,
    string? RawZone,
    string? RawVendor,
    string? RawModel,
    short? ResolvedTypeId,
    int? ResolvedGroupId,
    int? ResolvedParentDeviceId,
    string ValidationStatus,
    string? ValidationErrors,
    bool IsUserModified
);

public record StagedRecordsPagedResponse(
    int BatchId,
    IReadOnlyList<StagedDeviceDto> Data,
    PaginationMetadata Pagination
);

public record UpdateStagedRecordRequest(
    string? RawMacAddress = null,
    string? RawPrimaryIp = null,
    string? RawDisplayName = null,
    string? RawDeviceType = null,
    string? RawGroupPath = null,
    string? RawParentIdentifier = null,
    string? RawZone = null,
    string? RawVendor = null,
    string? RawModel = null,
    bool? IsMonitored = null
);

public record UpdateStagedRecordResponse(
    long StagedId,
    string? RawMacAddress,
    string ValidationStatus,
    string? ValidationErrors,
    bool IsUserModified
);

public record CommitBatchRequest(
    bool CommitValidOnly = true
);

public record CommitBatchResponse(
    int BatchId,
    string Status,
    int CommittedRowsCount,
    int RemainingIncompleteCount,
    DateTimeOffset CommittedAt
);
