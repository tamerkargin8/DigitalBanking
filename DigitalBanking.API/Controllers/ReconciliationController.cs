using System.Text;
using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Admin-only synthetic reconciliation. Upload is previewed and persisted first;
/// import is a separate, idempotent step. Results are never silently rewritten.
/// </summary>
[ApiController]
[Route("api/reconciliation")]
[Authorize]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _service;

    public ReconciliationController(IReconciliationService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    [Authorize(Roles = "Owner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(CsvStatementParser.MaxFileBytes + 4096)]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview(
        IFormFile file,
        [FromQuery] string? currency = null,
        [FromQuery] string? direction = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Failure("CSV dosyası zorunludur."));
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Failure("Yalnız .csv dosyası yüklenebilir."));
        if (file.Length > CsvStatementParser.MaxFileBytes)
            return BadRequest(ApiResponse.Failure($"Dosya boyutu {CsvStatementParser.MaxFileBytes / (1024 * 1024)} MB sınırını aşıyor."));

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var preview = await _service.PreviewAsync(
            stream.ToArray(), file.FileName, currency, direction, from, to,
            isDemo: false, User.GetActor(), cancellationToken);

        var message = preview.Batch.HasErrors
            ? "Dosya doğrulama hataları içeriyor; düzeltmeden içe aktarılamaz."
            : "Dosya önizlendi; içe aktarmadan önce satırları kontrol edin.";
        return Ok(new ApiResponse<ReconciliationPreviewDto>
        {
            Success = !preview.Batch.HasErrors,
            Data = preview,
            Message = message
        });
    }

    [HttpPost("{id:guid}/import")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationBatchDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _service.ImportAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationBatchDetailDto>.SuccessWith(detail, "Mutabakat içe aktarıldı."));
    }

    [HttpGet("batches")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ReconciliationBatchSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? currency = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetBatchesAsync(page, pageSize, status, currency, from, to, cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<ReconciliationBatchSummaryDto>>.SuccessWith(result));
    }

    [HttpGet("batches/{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationBatchDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatch(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _service.GetBatchAsync(id, cancellationToken);
        return Ok(ApiResponse<ReconciliationBatchDetailDto>.SuccessWith(detail));
    }

    [HttpGet("batches/{id:guid}/rows")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<List<ReconciliationRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRows(
        Guid id,
        [FromQuery] string? matchStatus = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _service.GetRowsAsync(id, matchStatus, cancellationToken);
        return Ok(ApiResponse<List<ReconciliationRowDto>>.SuccessWith(rows));
    }

    [HttpGet("batches/{id:guid}/export")]
    [Authorize(Roles = "Owner")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var csv = await _service.ExportCsvAsync(id, cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"reconciliation-{id:N}.csv");
    }

    /// <summary>Downloadable synthetic CSV examples (never real bank data).</summary>
    [HttpGet("sample")]
    [Authorize(Roles = "Owner")]
    [Produces("text/csv")]
    public IActionResult Sample([FromQuery] string kind = "matched")
    {
        var content = kind.Equals("mismatch", StringComparison.OrdinalIgnoreCase)
            ? SampleCsv.Mismatch
            : SampleCsv.Matched;
        return File(new UTF8Encoding(true).GetBytes(content), "text/csv; charset=utf-8", $"reconciliation-{kind}-sample.csv");
    }

    [HttpGet("exceptions")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ReconciliationExceptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExceptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? batchId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetExceptionsAsync(page, pageSize, status, batchId, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<ReconciliationExceptionDto>>.SuccessWith(result));
    }

    [HttpGet("exceptions/{id:guid}")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationExceptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetException(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetExceptionAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item));
    }

    [HttpPost("exceptions")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationExceptionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateException(
        [FromBody] CreateReconciliationExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _service.CreateExceptionAsync(request, User.GetActor(), cancellationToken);
        return CreatedAtAction(nameof(GetException), new { id = item.Id },
            ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "Mutabakat istisnası açıldı."));
    }

    [HttpPost("exceptions/{id:guid}/assign")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.AssignAsync(id, request.AssignedToUserId, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "İstisna atandı."));
    }

    [HttpPost("exceptions/{id:guid}/review")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.ReviewAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "İstisna incelemeye alındı."));
    }

    [HttpPost("exceptions/{id:guid}/propose")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Propose(Guid id, [FromBody] ProposeReconciliationResolutionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ProposeAsync(id, request.ResolutionNote, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "Çözüm önerisi kaydedildi."));
    }

    [HttpPost("exceptions/{id:guid}/approve")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.ApproveAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item,
            "İstisna kapatıldı. Bu operasyonel bir çözüm kaydıdır; bakiye düzeltmesi yapılmadı."));
    }

    [HttpPost("exceptions/{id:guid}/reject")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.RejectAsync(id, request.Reason, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "İstisna reddedildi."));
    }

    [HttpPost("exceptions/{id:guid}/reopen")]
    [Authorize(Roles = "Owner,DelegatedApprover")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ReopenAsync(id, request.Reason, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<ReconciliationExceptionDto>.SuccessWith(item, "İstisna yeniden açıldı."));
    }

    private static class SampleCsv
    {
        public const string Matched =
            "external_reference,internal_reference,amount,currency,transaction_date,direction\n" +
            "EXT-SAMPLE-001,11111111-1111-1111-1111-111111111111,100.00,TRY,2026-10-20,Debit\n" +
            "EXT-SAMPLE-002,11111111-1111-1111-1111-111111111112,100.00,TRY,2026-10-20,Credit\n";

        public const string Mismatch =
            "external_reference,internal_reference,amount,currency,transaction_date,direction\n" +
            "EXT-BAD-001,22222222-2222-2222-2222-222222222222,999.99,USD,2026-10-20,Debit\n" +
            "EXT-BAD-001,33333333-3333-3333-3333-333333333333,10.00,EUR,not-a-date,Debit\n";
    }
}
