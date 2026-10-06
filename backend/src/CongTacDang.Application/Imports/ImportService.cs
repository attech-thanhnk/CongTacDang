using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;

namespace CongTacDang.Application.Imports;

/// <summary>
/// Nhập dữ liệu từ Excel: tải file mẫu → tải file lên → xem trước từng dòng → xác nhận (ghi toàn bộ trong một transaction).
/// Có dòng lỗi thì không cho xác nhận (không nhập một phần).
/// </summary>
public interface IImportService
{
    /// <summary>Các loại nhập người dùng hiện tại được dùng.</summary>
    IReadOnlyList<ImportKindDto> GetKinds();

    /// <summary>File mẫu của loại nhập.</summary>
    Task<ImportFile> GetTemplateAsync(string kind, CancellationToken ct = default);

    /// <summary>Đọc, kiểm tra file và tạo phiên xem trước (hết hạn sau 30 phút).</summary>
    Task<ImportPreviewDto> PreviewAsync(string kind, Stream content, string? fileName, long length, CancellationToken ct = default);

    /// <summary>Xác nhận phiên: kiểm tra lại với dữ liệu hiện tại rồi ghi trong một transaction. Chỉ người tạo phiên được xác nhận.</summary>
    Task<ImportCommitResultDto> CommitAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Tải file kết quả (mật khẩu tạm) — một lần, chỉ người đã xác nhận.</summary>
    ImportFile DownloadResult(string token);
}

/// <summary>Triển khai <see cref="IImportService"/>.</summary>
public sealed class ImportService : IImportService
{
    private readonly IReadOnlyList<IImportDefinition> _definitions;
    private readonly IImportWorkbook _workbook;
    private readonly IImportSessionStore _sessions;
    private readonly IImportAuditWriter _audit;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Khởi tạo.</summary>
    public ImportService(
        IEnumerable<IImportDefinition> definitions,
        IImportWorkbook workbook,
        IImportSessionStore sessions,
        IImportAuditWriter audit,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _definitions = definitions.OrderBy(d => d.Order).ThenBy(d => d.Kind, StringComparer.Ordinal).ToList();
        _workbook = workbook;
        _sessions = sessions;
        _audit = audit;
        _guard = guard;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public IReadOnlyList<ImportKindDto> GetKinds()
    {
        if (!_guard.HasAny(PermissionCodes.SystemImport))
            return Array.Empty<ImportKindDto>();
        return _definitions
            .Where(d => d.RequiredPermissions.All(_guard.HasAny))
            .Select(d => new ImportKindDto
            {
                Kind = d.Kind,
                Name = d.Name,
                Description = d.Description,
                Columns = ToColumnDtos(d.Columns)
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ImportFile> GetTemplateAsync(string kind, CancellationToken ct = default)
    {
        var definition = Resolve(kind);
        var columns = await definition.GetTemplateColumnsAsync(ct);
        var bytes = _workbook.BuildTemplate(definition.Name, definition.Description, columns);
        return new ImportFile(bytes, ImportLimits.XlsxContentType, $"mau-nhap-{definition.Kind}.xlsx");
    }

    /// <inheritdoc />
    public async Task<ImportPreviewDto> PreviewAsync(string kind, Stream content, string? fileName, long length, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var definition = Resolve(kind);
        var ownerId = CurrentUserId();

        var name = Path.GetFileName(fileName ?? string.Empty);
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Chỉ nhận file Excel .xlsx. Hãy dùng file mẫu tải về từ trang này.");
        if (length <= 0)
            throw new ValidationException("File rỗng. Hãy chọn lại file.");
        if (length > ImportLimits.MaxFileBytes)
            throw new ValidationException($"File lớn quá {ImportLimits.MaxFileBytes / 1024 / 1024} MB. Hãy chia thành nhiều file nhỏ hơn.");

        var rows = _workbook.Read(content, definition.Columns);
        if (rows.Count == 0)
            throw new ValidationException("File không có dòng dữ liệu nào (dòng 1 là tiêu đề, dữ liệu bắt đầu từ dòng 2).");

        var analysis = await definition.AnalyzeAsync(rows, ct);
        var session = _sessions.Create(definition.Kind, ownerId, name, rows);
        return new ImportPreviewDto
        {
            SessionId = session.Id,
            Kind = definition.Kind,
            FileName = name,
            ExpiresAt = session.ExpiresAt,
            Columns = ToColumnDtos(definition.Columns),
            Rows = analysis.Rows.Select(r => new ImportPreviewRowDto
            {
                RowNumber = r.RowNumber,
                Action = ActionName(r.Action),
                Errors = r.Errors.ToList(),
                Notes = r.Notes.ToList(),
                Data = definition.Columns.ToDictionary(c => c.Key, c => r.Raw.Get(c.Key))
            }).ToList(),
            Summary = new ImportSummaryDto
            {
                Total = analysis.Rows.Count,
                Create = analysis.Rows.Count(r => r.Action == ImportRowAction.Create),
                Update = analysis.Rows.Count(r => r.Action == ImportRowAction.Update),
                Error = analysis.Rows.Count(r => r.Action == ImportRowAction.Error)
            }
        };
    }

    /// <inheritdoc />
    public async Task<ImportCommitResultDto> CommitAsync(Guid sessionId, CancellationToken ct = default)
    {
        var ownerId = CurrentUserId();
        var session = _sessions.Get(sessionId)
            ?? throw new NotFoundException("Phiên nhập không còn (đã xác nhận hoặc quá 30 phút). Hãy tải file lên lại.");
        if (session.OwnerId != ownerId)
            throw new ForbiddenException("Phiên nhập này do người khác tạo; chỉ người tải file lên mới xác nhận được.");
        var definition = Resolve(session.Kind);

        if (!_sessions.TryBeginCommit(sessionId))
            throw new ConflictException("Phiên nhập này đang được xác nhận. Hãy chờ kết quả.");

        var succeeded = false;
        try
        {
            ImportCommitOutcome? outcome = null;
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Kiểm tra lại với dữ liệu hiện tại (có thể đã đổi sau khi xem trước).
                var analysis = await definition.AnalyzeAsync(session.Rows, ct);
                var errors = analysis.Rows.Where(r => !r.IsValid).ToList();
                if (errors.Count > 0)
                {
                    var first = errors[0];
                    throw new ConflictException(
                        $"Còn {errors.Count} dòng lỗi nên chưa nhập dòng nào (dòng {first.RowNumber}: {first.Errors[0]}). "
                        + "Dữ liệu có thể đã thay đổi sau khi xem trước — hãy tải file lên lại để xem lỗi.");
                }

                outcome = await definition.CommitAsync(analysis, ct);
                _audit.AddSummary(definition.Kind, session.FileName, session.Rows.Count, outcome.Created, outcome.Updated);
                await _unitOfWork.SaveChangesAsync(ct);
            }, ct);

            var result = new ImportCommitResultDto
            {
                Kind = definition.Kind,
                Total = session.Rows.Count,
                Created = outcome!.Created,
                Updated = outcome.Updated
            };

            if (outcome.Credentials.Count > 0)
            {
                var bytes = _workbook.BuildTable(
                    "Tài khoản",
                    new[] { "Tên đăng nhập", "Họ và tên", "Mật khẩu tạm" },
                    outcome.Credentials.Select(c => (IReadOnlyList<string>)new[] { c.Username, c.FullName, c.TemporaryPassword }));
                var (token, expiresAt) = _sessions.SaveResult(ownerId,
                    new ImportFile(bytes, ImportLimits.XlsxContentType, $"mat-khau-tam-{DateTime.UtcNow.Add(ImportValues.VietnamOffset):yyyyMMdd-HHmm}.xlsx"));
                result.ResultFileToken = token;
                result.ResultFileExpiresAt = expiresAt;
            }

            succeeded = true;
            return result;
        }
        finally
        {
            _sessions.EndCommit(sessionId, succeeded);
        }
    }

    /// <inheritdoc />
    public ImportFile DownloadResult(string token)
    {
        var ownerId = CurrentUserId();
        return _sessions.TakeResult(token ?? string.Empty, ownerId)
            ?? throw new NotFoundException("File kết quả không còn: chỉ tải được một lần và trong 30 phút sau khi nhập.");
    }

    /// <summary>Loại nhập theo mã và kiểm tra quyền: <c>system.import</c> và quyền dữ liệu của loại.</summary>
    private IImportDefinition Resolve(string kind)
    {
        var definition = _definitions.FirstOrDefault(d => string.Equals(d.Kind, kind?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"Không có loại nhập dữ liệu \"{kind}\".");
        foreach (var code in new[] { PermissionCodes.SystemImport }.Concat(definition.RequiredPermissions))
        {
            if (!_guard.HasAny(code))
                throw new ForbiddenException($"Bạn không có quyền \"{PermissionCodes.DisplayName(code)}\" nên không nhập được {definition.Name.ToLowerInvariant()}.");
        }
        return definition;
    }

    private Guid CurrentUserId()
        => _currentUser.UserId ?? throw new ForbiddenException("Hãy đăng nhập để nhập dữ liệu.");

    private static List<ImportColumnDto> ToColumnDtos(IEnumerable<ImportColumn> columns)
        => columns.Select(c => new ImportColumnDto { Key = c.Key, Header = c.Header, Required = c.Required, Description = c.Description }).ToList();

    private static string ActionName(ImportRowAction action) => action switch
    {
        ImportRowAction.Create => "create",
        ImportRowAction.Update => "update",
        _ => "error"
    };
}
