using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Imports;

/// <summary>Khung nhập dữ liệu: liệt kê loại, file mẫu, xem trước, xác nhận, tải tệp kết quả.</summary>
public interface IImportService
{
    /// <summary>Các loại người dùng hiện tại được dùng (có <c>system.import</c> và mọi quyền dữ liệu của loại).</summary>
    Task<List<ImportKindDto>> GetKindsAsync(CancellationToken ct = default);

    /// <summary>File mẫu .xlsx của loại.</summary>
    Task<(string FileName, byte[] Content)> GetTemplateAsync(string kind, CancellationToken ct = default);

    /// <summary>Đọc tệp, phân tích từng dòng, tạo phiên xem trước.</summary>
    Task<ImportPreviewDto> PreviewAsync(string kind, string? fileName, long length, Stream content, CancellationToken ct = default);

    /// <summary>Phân tích lại và ghi toàn bộ trong một transaction; có dòng lỗi → không ghi dòng nào.</summary>
    Task<ImportCommitResultDto> CommitAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Lấy tệp kết quả (một lần); null nếu không còn.</summary>
    StoredImportResult? TakeResultFile(string token);
}

/// <summary>Triển khai khung nhập dữ liệu.</summary>
public sealed class ImportService : IImportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly Dictionary<string, IImportProcessor> _processors;
    private readonly IImportWorkbook _workbook;
    private readonly IImportSessionStore _sessions;
    private readonly IImportResultStore _results;
    private readonly IImportAuditLog _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionResolver _permissions;

    /// <summary>Khởi tạo khung; mã loại trùng nhau → lỗi cấu hình.</summary>
    public ImportService(
        IEnumerable<IImportProcessor> processors,
        IImportWorkbook workbook,
        IImportSessionStore sessions,
        IImportResultStore results,
        IImportAuditLog audit,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IPermissionResolver permissions)
    {
        _processors = new Dictionary<string, IImportProcessor>(StringComparer.OrdinalIgnoreCase);
        foreach (var processor in processors)
        {
            if (!_processors.TryAdd(processor.Definition.Kind, processor))
                throw new InvalidOperationException($"Loại import \"{processor.Definition.Kind}\" được đăng ký hai lần.");
        }

        _workbook = workbook;
        _sessions = sessions;
        _results = results;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    /// <summary>MIME của tệp .xlsx.</summary>
    public static string ContentType => XlsxContentType;

    /// <inheritdoc />
    public async Task<List<ImportKindDto>> GetKindsAsync(CancellationToken ct = default)
    {
        var effective = await _permissions.GetAsync(RequireUserId(), ct);
        if (!effective.Has(PermissionCodes.SystemImport))
            return new List<ImportKindDto>();

        return _processors.Values
            .Select(p => p.Definition)
            .Where(d => d.RequiredPermissions.All(effective.Has))
            .OrderBy(d => d.DisplayName, StringComparer.CurrentCulture)
            .Select(d => new ImportKindDto
            {
                Kind = d.Kind,
                DisplayName = d.DisplayName,
                Description = d.Description,
                Columns = ToColumnDtos(d)
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<(string FileName, byte[] Content)> GetTemplateAsync(string kind, CancellationToken ct = default)
    {
        var processor = await GetAuthorizedAsync(kind, ct);
        return ($"mau-nhap-{processor.Definition.Kind}.xlsx", _workbook.CreateTemplate(processor.Definition));
    }

    /// <inheritdoc />
    public async Task<ImportPreviewDto> PreviewAsync(string kind, string? fileName, long length, Stream content, CancellationToken ct = default)
    {
        var processor = await GetAuthorizedAsync(kind, ct);
        var userId = RequireUserId();

        if (length <= 0)
            throw new ValidationException("Tệp tải lên trống. Hãy chọn tệp Excel (.xlsx) đã điền dữ liệu.");
        if (length > ImportLimits.MaxFileBytes)
            throw new ValidationException("Tệp vượt quá 5 MB. Hãy tách thành nhiều tệp nhỏ hơn.");
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Chỉ nhận tệp Excel định dạng .xlsx. Hãy dùng file mẫu tải từ hệ thống.");

        var rows = _workbook.ReadRows(content, processor.Definition);
        var analysis = await processor.AnalyzeAsync(rows, ct);
        var session = _sessions.Create(processor.Definition.Kind, userId, Path.GetFileName(fileName), rows);

        return new ImportPreviewDto
        {
            SessionId = session.Id,
            Kind = processor.Definition.Kind,
            FileName = session.FileName,
            ExpiresAt = session.ExpiresAt,
            CanCommit = rows.Count > 0 && !analysis.HasErrors,
            Columns = ToColumnDtos(processor.Definition),
            Rows = analysis.Rows.Select(r => new ImportPreviewRowDto
            {
                RowNumber = r.RowNumber,
                Action = ActionName(r.Action),
                Errors = r.Errors.ToList(),
                Data = new Dictionary<string, string>(r.Data)
            }).ToList(),
            Summary = Summarize(analysis)
        };
    }

    /// <inheritdoc />
    public async Task<ImportCommitResultDto> CommitAsync(Guid sessionId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var peek = _sessions.Peek(sessionId)
            ?? throw new NotFoundException("Phiên nhập dữ liệu không tồn tại hoặc đã hết hạn (30 phút) hay đã được xác nhận. Hãy tải tệp lên lại.");
        if (peek.UserId != userId)
            throw new ForbiddenException("Chỉ người đã tải tệp lên mới được xác nhận phiên nhập dữ liệu này.");

        var processor = await GetAuthorizedAsync(peek.Kind, ct);
        var session = _sessions.Take(sessionId)
            ?? throw new NotFoundException("Phiên nhập dữ liệu đã hết hạn hoặc đã được xác nhận. Hãy tải tệp lên lại.");
        if (session.Rows.Count == 0)
            throw new ValidationException("Tệp không có dòng dữ liệu nào để nhập.");

        ImportCommitResult? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Phân tích lại trên dữ liệu hiện tại: CSDL có thể đã đổi kể từ lúc xem trước.
            var analysis = await processor.AnalyzeAsync(session.Rows, ct);
            if (analysis.HasErrors)
                throw new ValidationException(BuildErrorMessage(analysis));

            result = await processor.CommitAsync(analysis, ct);
            _audit.Add(processor.Definition.Kind, processor.Definition.DisplayName, session.FileName,
                session.Rows.Count, result.Created, result.Updated);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        var dto = new ImportCommitResultDto { Created = result!.Created, Updated = result.Updated };
        if (result.ResultFile != null)
        {
            var bytes = _workbook.CreateResultFile(result.ResultFile);
            var (token, expiresAt) = _results.Add(userId, result.ResultFile.FileName, bytes);
            dto.ResultFileToken = token;
            dto.ResultFileExpiresAt = expiresAt;
        }

        return dto;
    }

    /// <inheritdoc />
    public StoredImportResult? TakeResultFile(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        return _results.Take(token, RequireUserId());
    }

    private async Task<IImportProcessor> GetAuthorizedAsync(string kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kind) || !_processors.TryGetValue(kind, out var processor))
            throw new NotFoundException($"Không có loại nhập dữ liệu \"{kind}\". Hãy chọn loại trong danh sách.");

        var effective = await _permissions.GetAsync(RequireUserId(), ct);
        foreach (var code in processor.Definition.RequiredPermissions.Prepend(PermissionCodes.SystemImport))
        {
            if (!effective.Has(code))
                throw new ForbiddenException(
                    $"Bạn không có quyền \"{PermissionCodes.DisplayName(code)}\" để nhập \"{processor.Definition.DisplayName}\". "
                    + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }

        return processor;
    }

    private Guid RequireUserId()
        => _currentUser.UserId ?? throw new ForbiddenException("Không xác định được người dùng. Hãy đăng nhập lại.");

    private static List<ImportColumnDto> ToColumnDtos(IImportDefinition definition)
        => definition.TemplateColumns.Select(c => new ImportColumnDto
        {
            Key = c.Key,
            Header = c.Header,
            Required = c.Required,
            Description = c.Description,
            AllowedValues = c.AllowedValues?.ToList()
        }).ToList();

    private static string ActionName(ImportRowAction action) => action switch
    {
        ImportRowAction.Create => "create",
        ImportRowAction.Update => "update",
        _ => "error"
    };

    private static ImportSummaryDto Summarize(ImportAnalysis analysis) => new()
    {
        Total = analysis.Rows.Count,
        Create = analysis.Rows.Count(r => r.Action == ImportRowAction.Create),
        Update = analysis.Rows.Count(r => r.Action == ImportRowAction.Update),
        Error = analysis.Rows.Count(r => r.Action == ImportRowAction.Error)
    };

    private static string BuildErrorMessage(ImportAnalysis analysis)
    {
        var errorRows = analysis.Rows.Where(r => r.Action == ImportRowAction.Error).ToList();
        var samples = errorRows.Take(3).Select(r => $"dòng {r.RowNumber}: {string.Join(" ", r.Errors)}");
        return $"Không nhập dữ liệu vì có {errorRows.Count} dòng lỗi (dữ liệu có thể đã thay đổi kể từ lúc xem trước) — "
            + string.Join("; ", samples)
            + ". Không dòng nào được ghi. Hãy sửa tệp và tải lên lại.";
    }
}
