using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operational controller for the Low-Memory Streaming Bulk Ingestion Pipeline complying with
/// api_contracts_specification.md Section 9.
/// Boundary Rule: Controllers never inject DbContext; all operations route through IIngestService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class IngestController : ControllerBase
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB max file size
    private readonly IIngestService _ingestService;

    public IngestController(IIngestService ingestService)
    {
        _ingestService = ingestService;
    }

    /// <summary>
    /// Multipart CSV file upload to persistent staging.
    /// Spools file through 64KB ring buffer with zero full-heap loading.
    /// </summary>
    [HttpPost("upload")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(UploadBatchResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadBatch(
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidFileException("No file was uploaded or the uploaded file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidFileException("File size exceeds the 20MB maximum allowed limit.");
        }

        var userId = GetCallerUserId();
        await using var stream = file.OpenReadStream();

        var response = await _ingestService.UploadCsvStreamAsync(stream, file.FileName, userId, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Lists persistent staging batches.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpGet("batches")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(BatchesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBatches(CancellationToken ct)
    {
        var response = await _ingestService.GetBatchesAsync(ct);
        return Ok(response);
    }

    /// <summary>
    /// Keyset paginated retrieval of staged records for interactive operator grid.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpGet("batches/{batchId:int}/records")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(StagedRecordsPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStagedRecords(
        [FromRoute] int batchId,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var response = await _ingestService.GetStagedRecordsPagedAsync(batchId, status, page, limit, ct);
        return Ok(response);
    }

    /// <summary>
    /// Inline cell editing for staged records with automatic re-validation.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpPut("records/{stagedId:long}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(UpdateStagedRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStagedRecord(
        [FromRoute] long stagedId,
        [FromBody] UpdateStagedRecordRequest request,
        CancellationToken ct)
    {
        var response = await _ingestService.UpdateStagedRecordAsync(stagedId, request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Atomic bulk commit gatekeeper: commits valid staged rows into live production inventory within an ACID transaction.
    /// Restricted to NETWORK_ADMIN exclusively.
    /// </summary>
    [HttpPost("batches/{batchId:int}/commit")]
    [AuthorizeRole(RoleType.NetworkAdmin)]
    [ProducesResponseType(typeof(CommitBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CommitBatch(
        [FromRoute] int batchId,
        [FromBody] CommitBatchRequest request,
        CancellationToken ct)
    {
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var response = await _ingestService.CommitBatchAsync(batchId, request, userId, ipAddress, ct);
        return Ok(response);
    }

    /// <summary>
    /// Discards a staging batch and purges associated staged records.
    /// Restricted to NETWORK_ADMIN exclusively.
    /// </summary>
    [HttpDelete("batches/{batchId:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DiscardBatch(
        [FromRoute] int batchId,
        CancellationToken ct)
    {
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        await _ingestService.DiscardBatchAsync(batchId, userId, ipAddress, ct);
        return NoContent();
    }

    private int GetCallerUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out var id) ? id : 1;
    }
}
