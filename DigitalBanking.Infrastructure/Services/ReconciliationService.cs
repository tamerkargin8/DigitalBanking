using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Reconciliation;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Deterministic reference reconciliation for synthetic external statements.
/// Matching never guesses: duplicate references, amount/currency mismatches and
/// one-sided rows are reported as exceptions. Pending approvals are excluded from
/// "only system" and counted separately. Closing an exception never changes money.
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly BankDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IApprovalAuthorizationService _authorization;
    private readonly ILogger _logger;

    public ReconciliationService(
        BankDbContext context,
        IAuditService auditService,
        IApprovalAuthorizationService authorization)
    {
        _context = context;
        _auditService = auditService;
        _authorization = authorization;
        _logger = Log.ForContext<ReconciliationService>();
    }

    public async Task<ReconciliationPreviewDto> PreviewAsync(
        byte[] content,
        string fileName,
        string? currency,
        string? direction,
        DateTime? from,
        DateTime? to,
        bool isDemo,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(actor, cancellationToken);

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : CurrencyCodes.Normalize(currency);
        var normalizedDirection = ParseDirectionOrNull(direction);
        var (scopeFrom, scopeTo) = NormalizeScopeDates(from, to);
        var scopeKey = BuildScopeKey(normalizedCurrency, normalizedDirection, scopeFrom, scopeTo);
        var hash = Convert.ToHexString(SHA256.HashData(content));

        var existing = await LoadBatchByFingerprintAsync(hash, scopeKey, cancellationToken);
        if (existing is not null)
        {
            var existingRows = await _context.ReconciliationRows.AsNoTracking()
                .Where(x => x.BatchId == existing.Id)
                .OrderBy(x => x.LineNumber)
                .ToListAsync(cancellationToken);
            return new ReconciliationPreviewDto
            {
                Batch = MapBatch(existing),
                Replayed = true,
                InvalidRowCount = existingRows.Count(x => !x.IsValid),
                Rows = existingRows.Select(MapRow).ToList()
            };
        }

        var parse = CsvStatementParser.Parse(content);
        var batch = new ReconciliationBatch
        {
            Id = Guid.NewGuid(),
            FileName = SafeFileName(fileName),
            FileHash = hash,
            SizeBytes = content.LongLength,
            ScopeKey = scopeKey,
            CurrencyScope = normalizedCurrency,
            DirectionScope = normalizedDirection,
            FromDate = scopeFrom,
            ToDate = scopeTo,
            ImportedByUserId = actor.UserId,
            ImportedByUsername = actor.Username,
            IsDemo = isDemo,
            CreatedAt = DateTime.UtcNow
        };

        if (parse.FatalError is not null)
        {
            batch.Status = ReconciliationBatchStatus.Invalid;
            batch.HasErrors = true;
            batch.ValidationSummary = parse.FatalError;
            _context.ReconciliationBatches.Add(batch);
            _auditService.Add("RECONCILIATION_PREVIEW_INVALID", "ReconciliationBatch", batch.Id.ToString(),
                actor.Username, false, $"File: {batch.FileName}. Error: {parse.FatalError}");
            await _context.SaveChangesAsync(cancellationToken);
            return new ReconciliationPreviewDto { Batch = MapBatch(batch) };
        }

        var effectiveFrom = scopeFrom ?? (parse.Rows.Count == 0 ? null : parse.Rows.Min(x => x.TransactionDate).Date);
        var effectiveTo = scopeTo ?? (parse.Rows.Count == 0 ? null : parse.Rows.Max(x => x.TransactionDate).Date.AddDays(1).AddTicks(-1));
        batch.FromDate = effectiveFrom;
        batch.ToDate = effectiveTo;
        batch.RowCount = parse.Rows.Count;

        foreach (var row in parse.Rows)
        {
            var validationError = row.Error;
            if (validationError is null && normalizedCurrency is not null
                && !string.Equals(row.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
                validationError = $"Satır kapsam dışı: currency {normalizedCurrency} bekleniyor.";
            if (validationError is null && normalizedDirection is not null && row.Direction != normalizedDirection)
                validationError = $"Satır kapsam dışı: direction {normalizedDirection} bekleniyor.";
            if (validationError is null && effectiveFrom is not null && row.TransactionDate.Date < effectiveFrom.Value.Date)
                validationError = "Satır seçili tarih aralığının dışında.";
            if (validationError is null && effectiveTo is not null && row.TransactionDate.Date > effectiveTo.Value.Date)
                validationError = "Satır seçili tarih aralığının dışında.";

            batch.Rows.Add(new ReconciliationRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LineNumber = row.LineNumber,
                ExternalReference = row.ExternalReference,
                InternalReference = row.InternalReference,
                Amount = row.Amount,
                Currency = row.Currency,
                TransactionDate = row.TransactionDate,
                Direction = row.Direction,
                IsValid = validationError is null,
                ValidationError = validationError,
                MatchStatus = ReconciliationMatchStatus.Unmatched
            });
        }

        var invalid = batch.Rows.Count(x => !x.IsValid);
        batch.HasErrors = invalid > 0;
        batch.Status = invalid > 0 ? ReconciliationBatchStatus.Invalid : ReconciliationBatchStatus.Previewed;
        batch.ValidationSummary = invalid > 0
            ? $"{invalid} satırda doğrulama hatası var. Düzeltmeden içe aktarılamaz."
            : null;

        _context.ReconciliationBatches.Add(batch);
        _auditService.Add(
            invalid > 0 ? "RECONCILIATION_PREVIEW_INVALID" : "RECONCILIATION_PREVIEWED",
            "ReconciliationBatch",
            batch.Id.ToString(),
            actor.Username,
            invalid == 0,
            $"File: {batch.FileName}, Rows: {batch.RowCount}, Invalid: {invalid}, Scope: {batch.ScopeKey}");

        await _context.SaveChangesAsync(cancellationToken);

        return new ReconciliationPreviewDto
        {
            Batch = MapBatch(batch),
            InvalidRowCount = invalid,
            Rows = batch.Rows.OrderBy(x => x.LineNumber).Select(MapRow).ToList()
        };
    }

    public async Task<ReconciliationBatchDetailDto> ImportAsync(Guid batchId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(actor, cancellationToken);

        var initial = await _context.ReconciliationBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (initial is null)
            throw new NotFoundException("ReconciliationBatch", batchId.ToString());

        if (initial.Status == ReconciliationBatchStatus.Imported)
            return await GetBatchAsync(batchId, cancellationToken);

        if (initial.Status == ReconciliationBatchStatus.Invalid || initial.HasErrors)
            throw new BusinessRuleException("Dosyada doğrulama hataları var; içe aktarılamaz. Hatalı satırları düzeltip yeniden yükleyin.");

        if (initial.Status == ReconciliationBatchStatus.Importing)
            throw new BusinessRuleException("Bu batch zaten içe aktarılıyor.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var affected = await _context.ReconciliationBatches
                .Where(x => x.Id == batchId && x.Status == ReconciliationBatchStatus.Previewed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, ReconciliationBatchStatus.Importing), cancellationToken);

            if (affected == 0)
            {
                var current = await _context.ReconciliationBatches.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
                if (current?.Status == ReconciliationBatchStatus.Imported)
                    return await GetBatchAsync(batchId, cancellationToken);
                throw new BusinessRuleException("Batch durumu değişti; sayfayı yenileyip tekrar deneyin.");
            }

            _context.ChangeTracker.Clear();
            var batch = await _context.ReconciliationBatches
                .Include(x => x.Rows)
                .FirstAsync(x => x.Id == batchId, cancellationToken);

            var from = batch.FromDate ?? batch.Rows.Min(x => x.TransactionDate).Date;
            var to = batch.ToDate ?? batch.Rows.Max(x => x.TransactionDate).Date.AddDays(1).AddTicks(-1);

            var system = await LoadSystemTransactionsAsync(from, to, batch.CurrencyScope, batch.DirectionScope, cancellationToken);
            var pendingApprovals = await _context.PaymentApprovals.AsNoTracking()
                .Where(x => x.Status == PaymentApprovalStatus.Pending
                            && x.CreatedAt >= from && x.CreatedAt <= to
                            && (batch.CurrencyScope == null || x.Currency == batch.CurrencyScope))
                .CountAsync(cancellationToken);

            Match(batch, system);

            batch.Status = ReconciliationBatchStatus.Imported;
            batch.ImportedAt = DateTime.UtcNow;
            batch.HasErrors = false;
            batch.ValidationSummary = null;
            batch.PendingApprovalCount = pendingApprovals;

            _auditService.Add(
                "RECONCILIATION_IMPORTED",
                "ReconciliationBatch",
                batch.Id.ToString(),
                actor.Username,
                true,
                $"File: {batch.FileName}. Matched: {batch.MatchedCount}, OnlyExternal: {batch.OnlyExternalCount}, " +
                $"OnlySystem: {batch.OnlySystemCount}, AmountMismatch: {batch.AmountMismatchCount}, " +
                $"CurrencyMismatch: {batch.CurrencyMismatchCount}, Duplicate: {batch.DuplicateReferenceCount}, " +
                $"PendingApprovals: {batch.PendingApprovalCount}");

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Reconciliation batch {BatchId} imported. Matched: {Matched}, OnlySystem: {OnlySystem}",
                batch.Id, batch.MatchedCount, batch.OnlySystemCount);

            return await GetBatchAsync(batchId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Reconciliation import concurrency failure.");
            await SafeRollbackAsync(transaction, cancellationToken);
            throw new ConcurrencyException($"Reconciliation batch {batchId} was imported by another request.");
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<PaginatedResponse<ReconciliationBatchSummaryDto>> GetBatchesAsync(
        int page, int pageSize, string? status, string? currency, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.ReconciliationBatches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ReconciliationBatchStatus>(status, ignoreCase: true, out var parsed))
                throw new BusinessRuleException("Geçersiz batch durumu.");
            query = query.Where(x => x.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = CurrencyCodes.Normalize(currency);
            query = query.Where(x => x.CurrencyScope == normalized);
        }

        if (from is not null) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to is not null) query = query.Where(x => x.CreatedAt <= to.Value.Date.AddDays(1).AddTicks(-1));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<ReconciliationBatchSummaryDto>(
            items.Select(MapBatch).ToList(), page, pageSize, total);
    }

    public async Task<ReconciliationBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _context.ReconciliationBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (batch is null)
            throw new NotFoundException("ReconciliationBatch", batchId.ToString());

        var rows = await _context.ReconciliationRows.AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.LineNumber)
            .ThenBy(x => x.ExternalReference)
            .ToListAsync(cancellationToken);

        var exceptions = await QueryExceptionsAsync(null, batchId, cancellationToken);

        return new ReconciliationBatchDetailDto
        {
            Batch = MapBatch(batch),
            Rows = rows.Select(MapRow).ToList(),
            Exceptions = exceptions
        };
    }

    public async Task<List<ReconciliationRowDto>> GetRowsAsync(Guid batchId, string? matchStatus, CancellationToken cancellationToken = default)
    {
        var query = _context.ReconciliationRows.AsNoTracking().Where(x => x.BatchId == batchId);
        if (!string.IsNullOrWhiteSpace(matchStatus))
        {
            if (!Enum.TryParse<ReconciliationMatchStatus>(matchStatus, ignoreCase: true, out var parsed))
                throw new BusinessRuleException("Geçersiz eşleşme durumu.");
            query = query.Where(x => x.MatchStatus == parsed);
        }

        var rows = await query.OrderBy(x => x.LineNumber).ThenBy(x => x.ExternalReference)
            .ToListAsync(cancellationToken);
        return rows.Select(MapRow).ToList();
    }

    public async Task<string> ExportCsvAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _context.ReconciliationBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (batch is null)
            throw new NotFoundException("ReconciliationBatch", batchId.ToString());

        var rows = await _context.ReconciliationRows.AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.LineNumber)
            .ThenBy(x => x.ExternalReference)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine("line_number,external_reference,internal_reference,amount,currency,transaction_date,direction,match_status,matched_transaction_id,system_amount,system_currency,system_date,difference_amount,validation_error");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                row.LineNumber.ToString(CultureInfo.InvariantCulture),
                CsvCell(row.ExternalReference),
                CsvCell(row.InternalReference),
                row.Amount.ToString("F2", CultureInfo.InvariantCulture),
                CsvCell(row.Currency),
                row.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CsvCell(row.Direction.ToString()),
                CsvCell(row.MatchStatus.ToString()),
                row.MatchedTransactionId?.ToString() ?? string.Empty,
                row.SystemAmount?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                CsvCell(row.SystemCurrency),
                row.SystemDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                row.DifferenceAmount?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                CsvCell(row.ValidationError)));
        }

        return builder.ToString();
    }

    public async Task<PaginatedResponse<ReconciliationExceptionDto>> GetExceptionsAsync(
        int page, int pageSize, string? status, Guid? batchId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.ReconciliationExceptions.AsNoTracking();
        if (batchId is not null) query = query.Where(x => x.BatchId == batchId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ReconciliationExceptionStatus>(status, ignoreCase: true, out var parsed))
                throw new BusinessRuleException("Geçersiz istisna durumu.");
            query = query.Where(x => x.Status == parsed);
        }

        var total = await query.CountAsync(cancellationToken);
        var ids = await query.OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => x.Id).ToListAsync(cancellationToken);

        var items = await QueryExceptionsAsync(null, null, cancellationToken, ids);
        return new PaginatedResponse<ReconciliationExceptionDto>(items, page, pageSize, total);
    }

    public async Task<ReconciliationExceptionDto> GetExceptionAsync(Guid id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var items = await QueryExceptionsAsync(id, null, cancellationToken);
        var item = items.FirstOrDefault() ?? throw new NotFoundException("ReconciliationException", id.ToString());
        return item;
    }

    public async Task<ReconciliationExceptionDto> CreateExceptionAsync(
        CreateReconciliationExceptionRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length < 5 || description.Length > 1000)
            throw new BusinessRuleException("Açıklama 5–1000 karakter arasında olmalıdır.");

        var row = await _context.ReconciliationRows
            .Include(x => x.Batch)
            .FirstOrDefaultAsync(x => x.Id == request.RowId, cancellationToken);
        if (row is null)
            throw new NotFoundException("ReconciliationRow", request.RowId.ToString());
        if (row.Batch.Status != ReconciliationBatchStatus.Imported)
            throw new BusinessRuleException("Yalnız içe aktarılmış batch satırından istisna açılabilir.");
        if (row.MatchStatus == ReconciliationMatchStatus.Matched)
            throw new BusinessRuleException("Eşleşen satır için istisna açılamaz.");
        if (!row.IsValid)
            throw new BusinessRuleException("Geçersiz satır için istisna açılamaz; önce dosyayı düzeltin.");

        var assignedUsername = await ResolveUsernameAsync(request.AssignedToUserId, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ReconciliationException
        {
            Id = Guid.NewGuid(),
            BatchId = row.BatchId,
            RowId = row.Id,
            ExternalReference = row.ExternalReference,
            InternalReference = row.InternalReference,
            Status = ReconciliationExceptionStatus.Open,
            Description = description,
            AssignedToUserId = request.AssignedToUserId,
            AssignedToUsername = assignedUsername,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ReconciliationExceptions.Add(entity);
        _auditService.Add("RECON_EXCEPTION_CREATED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, $"Batch: {row.BatchId}, ExternalRef: {row.ExternalReference}, Status: Open");
        await _context.SaveChangesAsync(cancellationToken);
        return await GetExceptionAsync(entity.Id, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> AssignAsync(
        Guid id, Guid? assignedToUserId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status is ReconciliationExceptionStatus.Closed or ReconciliationExceptionStatus.Rejected)
            throw new BusinessRuleException("Kapanmış/reddedilmiş istisna atanamaz.");

        var username = await ResolveUsernameAsync(assignedToUserId, cancellationToken);
        entity.AssignedToUserId = assignedToUserId;
        entity.AssignedToUsername = username;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_ASSIGNED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, $"AssignedTo: {username ?? "none"}");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> ReviewAsync(Guid id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status != ReconciliationExceptionStatus.Open)
            throw new BusinessRuleException("Yalnız Açık istisna İncelemede durumuna alınabilir.");

        entity.Status = ReconciliationExceptionStatus.InReview;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_IN_REVIEW", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, "Status: Open -> InReview");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> ProposeAsync(
        Guid id, string resolutionNote, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var note = (resolutionNote ?? string.Empty).Trim();
        if (note.Length < 5 || note.Length > 1000)
            throw new BusinessRuleException("Çözüm gerekçesi 5–1000 karakter arasında olmalıdır.");

        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status != ReconciliationExceptionStatus.InReview)
            throw new BusinessRuleException("Çözüm önerisi yalnız İncelemede durumundan yapılabilir.");

        entity.Status = ReconciliationExceptionStatus.ResolutionProposed;
        entity.ResolutionNote = note;
        entity.ProposedByUserId = actor.UserId;
        entity.ProposedByUsername = actor.Username;
        entity.ProposedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_PROPOSED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, $"Status: InReview -> ResolutionProposed. Note: {note}");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> ApproveAsync(Guid id, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status != ReconciliationExceptionStatus.ResolutionProposed)
            throw new BusinessRuleException("Yalnız çözüm önerilmiş istisna kapatılabilir.");
        if (entity.ProposedByUserId == actor.UserId)
            throw new BusinessRuleException("Çözümü öneren kişi kendi çözümünü onaylayamaz.");

        entity.Status = ReconciliationExceptionStatus.Closed;
        entity.ReviewedByUserId = actor.UserId;
        entity.ReviewedByUsername = actor.Username;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_CLOSED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, "Status: ResolutionProposed -> Closed. Operational resolution only; no balance change.");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> RejectAsync(
        Guid id, string reason, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var trimmed = (reason ?? string.Empty).Trim();
        if (trimmed.Length < 3 || trimmed.Length > 500)
            throw new BusinessRuleException("Red gerekçesi 3–500 karakter arasında olmalıdır.");

        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status != ReconciliationExceptionStatus.ResolutionProposed)
            throw new BusinessRuleException("Yalnız çözüm önerilmiş istisna reddedilebilir.");
        if (entity.ProposedByUserId == actor.UserId)
            throw new BusinessRuleException("Çözümü öneren kişi kendi çözümünü reddedemez.");

        entity.Status = ReconciliationExceptionStatus.Rejected;
        entity.ReviewedByUserId = actor.UserId;
        entity.ReviewedByUsername = actor.Username;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.RejectionReason = trimmed;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_REJECTED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, $"Status: ResolutionProposed -> Rejected. Reason: {trimmed}");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    public async Task<ReconciliationExceptionDto> ReopenAsync(
        Guid id, string? reason, ActorContext actor, CancellationToken cancellationToken = default)
    {
        await EnsureExceptionAuthorizedAsync(actor, cancellationToken);
        var entity = await LoadExceptionAsync(id, cancellationToken);
        if (entity.Status is not (ReconciliationExceptionStatus.Closed or ReconciliationExceptionStatus.Rejected))
            throw new BusinessRuleException("Yalnız kapanmış/reddedilmiş istisna yeniden açılabilir.");

        entity.Status = ReconciliationExceptionStatus.Open;
        entity.ResolutionNote = null;
        entity.ProposedByUserId = null;
        entity.ProposedByUsername = null;
        entity.ProposedAt = null;
        entity.ReviewedByUserId = null;
        entity.ReviewedByUsername = null;
        entity.ReviewedAt = null;
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        _auditService.Add("RECON_EXCEPTION_REOPENED", "ReconciliationException", entity.Id.ToString(),
            actor.Username, true, $"Status: {entity.Status} -> Open. Reason: {reason ?? "none"}");
        return await SaveExceptionAsync(entity, actor, cancellationToken);
    }

    private async Task<ReconciliationExceptionDto> SaveExceptionAsync(ReconciliationException entity, ActorContext actor, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException($"Reconciliation exception {entity.Id} was updated by another Admin.");
        }

        return await GetExceptionAsync(entity.Id, actor, cancellationToken);
    }

    private async Task<ReconciliationException> LoadExceptionAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ReconciliationExceptions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("ReconciliationException", id.ToString());
    }

    private async Task<string?> ResolveUsernameAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null) return null;
        var username = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => x.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (username is null)
            throw new BusinessRuleException("Atanacak kullanıcı bulunamadı.");
        return username;
    }

    private async Task<List<ReconciliationExceptionDto>> QueryExceptionsAsync(
        Guid? id, Guid? batchId, CancellationToken cancellationToken, List<Guid>? ids = null)
    {
        var query = _context.ReconciliationExceptions.AsNoTracking();
        if (id is not null) query = query.Where(x => x.Id == id.Value);
        if (batchId is not null) query = query.Where(x => x.BatchId == batchId.Value);
        if (ids is not null) query = query.Where(x => ids.Contains(x.Id));

        var rows = await query
            .Join(_context.ReconciliationBatches.AsNoTracking(),
                exception => exception.BatchId,
                batch => batch.Id,
                (exception, batch) => new { exception, batch })
            .OrderByDescending(x => x.exception.UpdatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapException(x.exception, x.batch.FileName)).ToList();
    }

    private async Task<ReconciliationBatch?> LoadBatchByFingerprintAsync(string hash, string scopeKey, CancellationToken cancellationToken)
    {
        return await _context.ReconciliationBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FileHash == hash && x.ScopeKey == scopeKey, cancellationToken);
    }

    private async Task<List<SystemTransaction>> LoadSystemTransactionsAsync(
        DateTime from, DateTime to, string? currency, ReconciliationDirection? direction, CancellationToken cancellationToken)
    {
        var query = _context.Transactions.AsNoTracking()
            .Where(x => x.CorrelationId != null && x.CreatedDate >= from && x.CreatedDate <= to)
            .Join(_context.Accounts.AsNoTracking(),
                transaction => transaction.AccountId,
                account => account.Id,
                (transaction, account) => new { transaction, account })
            .Where(x => currency == null || x.account.Currency == currency)
            .Select(x => new SystemTransaction
            {
                TransactionId = x.transaction.Id,
                CorrelationId = x.transaction.CorrelationId!.Value,
                Amount = x.transaction.Amount,
                Currency = x.account.Currency,
                CreatedDate = x.transaction.CreatedDate,
                Direction = x.transaction.Type == TransactionType.Withdraw || x.transaction.Type == TransactionType.TransferOut
                    ? ReconciliationDirection.Debit
                    : ReconciliationDirection.Credit
            });

        var items = await query.ToListAsync(cancellationToken);
        return direction is null ? items : items.Where(x => x.Direction == direction).ToList();
    }

    private void Match(ReconciliationBatch batch, List<SystemTransaction> system)
    {
        var externalRows = batch.Rows.Where(x => x.IsValid).ToList();

        var duplicateExternal = externalRows
            .GroupBy(x => x.ExternalReference, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToHashSet();

        var duplicateInternal = externalRows
            .GroupBy(x => (x.InternalReference, x.Direction))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToHashSet();

        var byTransactionId = system.ToDictionary(x => x.TransactionId);
        var byCorrelation = system.GroupBy(x => x.CorrelationId).ToDictionary(g => g.Key, g => g.ToList());
        var matchedTransactionIds = new HashSet<Guid>();

        foreach (var row in externalRows)
        {
            if (duplicateExternal.Contains(row) || duplicateInternal.Contains(row))
            {
                row.MatchStatus = ReconciliationMatchStatus.DuplicateReference;
                continue;
            }

            Guid internalGuid;
            if (!Guid.TryParse(row.InternalReference, out internalGuid))
            {
                row.MatchStatus = ReconciliationMatchStatus.OnlyExternal;
                continue;
            }

            List<SystemTransaction> candidates;
            if (byTransactionId.TryGetValue(internalGuid, out var byId))
                candidates = new List<SystemTransaction> { byId };
            else if (byCorrelation.TryGetValue(internalGuid, out var byCorr))
                candidates = byCorr;
            else
                candidates = new List<SystemTransaction>();

            candidates = candidates.Where(x => x.Direction == row.Direction).ToList();

            if (candidates.Count == 0)
            {
                row.MatchStatus = ReconciliationMatchStatus.OnlyExternal;
                continue;
            }

            if (candidates.Count > 1)
            {
                row.MatchStatus = ReconciliationMatchStatus.DuplicateReference;
                continue;
            }

            var candidate = candidates[0];
            row.MatchedTransactionId = candidate.TransactionId;
            row.SystemAmount = candidate.Amount;
            row.SystemCurrency = candidate.Currency;
            row.SystemDate = candidate.CreatedDate;

            if (!string.Equals(candidate.Currency, row.Currency, StringComparison.OrdinalIgnoreCase))
            {
                row.MatchStatus = ReconciliationMatchStatus.CurrencyMismatch;
                continue;
            }

            if (candidate.Amount != row.Amount)
            {
                row.DifferenceAmount = row.Amount - candidate.Amount;
                row.MatchStatus = ReconciliationMatchStatus.AmountMismatch;
                continue;
            }

            if (!matchedTransactionIds.Add(candidate.TransactionId))
            {
                row.MatchStatus = ReconciliationMatchStatus.DuplicateReference;
                continue;
            }

            row.MatchStatus = ReconciliationMatchStatus.Matched;
        }

        var onlySystemRows = system.Where(x => !matchedTransactionIds.Contains(x.TransactionId)).ToList();
        foreach (var unmatched in onlySystemRows)
        {
            _context.ReconciliationRows.Add(new ReconciliationRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LineNumber = 0,
                ExternalReference = $"SYS-{unmatched.TransactionId:N}",
                InternalReference = unmatched.CorrelationId.ToString("D"),
                Amount = unmatched.Amount,
                Currency = unmatched.Currency,
                TransactionDate = unmatched.CreatedDate,
                Direction = unmatched.Direction,
                IsValid = true,
                MatchStatus = ReconciliationMatchStatus.OnlySystem,
                MatchedTransactionId = unmatched.TransactionId,
                SystemAmount = unmatched.Amount,
                SystemCurrency = unmatched.Currency,
                SystemDate = unmatched.CreatedDate
            });
        }

        batch.MatchedCount = externalRows.Count(x => x.MatchStatus == ReconciliationMatchStatus.Matched);
        batch.OnlyExternalCount = externalRows.Count(x => x.MatchStatus == ReconciliationMatchStatus.OnlyExternal);
        batch.AmountMismatchCount = externalRows.Count(x => x.MatchStatus == ReconciliationMatchStatus.AmountMismatch);
        batch.CurrencyMismatchCount = externalRows.Count(x => x.MatchStatus == ReconciliationMatchStatus.CurrencyMismatch);
        batch.DuplicateReferenceCount = externalRows.Count(x => x.MatchStatus == ReconciliationMatchStatus.DuplicateReference);
        batch.OnlySystemCount = onlySystemRows.Count;
    }

    private static ReconciliationBatchSummaryDto MapBatch(ReconciliationBatch batch) => new()
    {
        Id = batch.Id,
        FileName = batch.FileName,
        FileHash = batch.FileHash,
        SizeBytes = batch.SizeBytes,
        RowCount = batch.RowCount,
        ScopeKey = batch.ScopeKey,
        CurrencyScope = batch.CurrencyScope,
        DirectionScope = batch.DirectionScope?.ToString(),
        FromDate = batch.FromDate,
        ToDate = batch.ToDate,
        Status = batch.Status.ToString(),
        HasErrors = batch.HasErrors,
        ValidationSummary = batch.ValidationSummary,
        IsDemo = batch.IsDemo,
        ImportedByUsername = batch.ImportedByUsername,
        CreatedAt = batch.CreatedAt,
        ImportedAt = batch.ImportedAt,
        MatchedCount = batch.MatchedCount,
        OnlyExternalCount = batch.OnlyExternalCount,
        OnlySystemCount = batch.OnlySystemCount,
        AmountMismatchCount = batch.AmountMismatchCount,
        CurrencyMismatchCount = batch.CurrencyMismatchCount,
        DuplicateReferenceCount = batch.DuplicateReferenceCount,
        PendingApprovalCount = batch.PendingApprovalCount
    };

    private static ReconciliationRowDto MapRow(ReconciliationRow row) => new()
    {
        Id = row.Id,
        LineNumber = row.LineNumber,
        ExternalReference = row.ExternalReference,
        InternalReference = row.InternalReference,
        Amount = row.Amount,
        Currency = row.Currency,
        TransactionDate = row.TransactionDate,
        Direction = row.Direction.ToString(),
        IsValid = row.IsValid,
        ValidationError = row.ValidationError,
        MatchStatus = row.MatchStatus.ToString(),
        MatchedTransactionId = row.MatchedTransactionId,
        SystemAmount = row.SystemAmount,
        SystemCurrency = row.SystemCurrency,
        SystemDate = row.SystemDate,
        DifferenceAmount = row.DifferenceAmount
    };

    private static ReconciliationExceptionDto MapException(ReconciliationException entity, string batchFileName) => new()
    {
        Id = entity.Id,
        BatchId = entity.BatchId,
        BatchFileName = batchFileName,
        RowId = entity.RowId,
        ExternalReference = entity.ExternalReference,
        InternalReference = entity.InternalReference,
        Status = entity.Status.ToString(),
        Description = entity.Description,
        AssignedToUserId = entity.AssignedToUserId,
        AssignedToUsername = entity.AssignedToUsername,
        ResolutionNote = entity.ResolutionNote,
        ProposedByUserId = entity.ProposedByUserId,
        ProposedByUsername = entity.ProposedByUsername,
        ProposedAt = entity.ProposedAt,
        ReviewedByUserId = entity.ReviewedByUserId,
        ReviewedByUsername = entity.ReviewedByUsername,
        ReviewedAt = entity.ReviewedAt,
        RejectionReason = entity.RejectionReason,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            text = "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (name.Length == 0) name = "statement.csv";
        return name.Length > 255 ? name[..255] : name;
    }

    private static (DateTime? From, DateTime? To) NormalizeScopeDates(DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return (null, null);
        if (from is not null && to is not null)
        {
            var normalized = ReportDateRange.Normalize(from.Value, to.Value);
            return (normalized.From, normalized.To);
        }

        if (from is not null)
            return (from.Value, from.Value.Date.AddDays(1).AddTicks(-1));
        return (to!.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1));
    }

    private static string BuildScopeKey(string? currency, ReconciliationDirection? direction, DateTime? from, DateTime? to)
    {
        var c = currency ?? "ALL";
        var d = direction?.ToString() ?? "ALL";
        var f = from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "ALL";
        var t = to?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "ALL";
        return $"{c}|{d}|{f}|{t}";
    }

    private static ReconciliationDirection? ParseDirectionOrNull(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction)) return null;
        if (!Enum.TryParse<ReconciliationDirection>(direction.Trim(), ignoreCase: true, out var parsed))
            throw new BusinessRuleException("direction Debit veya Credit olmalı.");
        return parsed;
    }

    private async Task EnsureOwnerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var active = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (active.Role != UserRole.Owner)
            throw new ForbiddenException("Mutabakat dosya yönetimi yalnız Owner rolüne açıktır.");
    }

    private async Task EnsureExceptionAuthorizedAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var active = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (active.Role == UserRole.Owner) return;
        var permission = await _authorization.CheckAsync(
            active.UserId, ApprovalOperation.ReconciliationException, null, null, cancellationToken);
        if (!permission.Allowed)
            throw new ForbiddenException(permission.Reason);
    }

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Transaction may already be completed after a commit/rollback.
        }
    }

    private sealed class SystemTransaction
    {
        public Guid TransactionId { get; init; }
        public Guid CorrelationId { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public DateTime CreatedDate { get; init; }
        public ReconciliationDirection Direction { get; init; }
    }
}
