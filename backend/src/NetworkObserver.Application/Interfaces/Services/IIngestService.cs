using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for the Low-Memory Streaming Bulk Ingestion Pipeline.
/// Complies with api_contracts_specification.md Section 9 and SOLID principles.
/// </summary>
public interface IIngestService
{
    Task<UploadBatchResponse> UploadCsvStreamAsync(
        Stream fileStream,
        string filename,
        int userId,
        CancellationToken ct = default);

    Task<BatchesListResponse> GetBatchesAsync(CancellationToken ct = default);

    Task<StagedRecordsPagedResponse> GetStagedRecordsPagedAsync(
        int batchId,
        string? status,
        int page,
        int limit,
        CancellationToken ct = default);

    Task<UpdateStagedRecordResponse> UpdateStagedRecordAsync(
        long stagedId,
        UpdateStagedRecordRequest request,
        int? userId = null,
        string ipAddress = "127.0.0.1",
        CancellationToken ct = default);

    Task<CommitBatchResponse> CommitBatchAsync(
        int batchId,
        CommitBatchRequest request,
        int userId,
        string ipAddress,
        CancellationToken ct = default);

    Task DiscardBatchAsync(
        int batchId,
        int userId,
        string ipAddress,
        CancellationToken ct = default);
}
