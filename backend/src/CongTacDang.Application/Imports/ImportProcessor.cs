namespace CongTacDang.Application.Imports;

/// <summary>Kết quả phân tích một dòng (không phụ thuộc kiểu dòng) — dùng cho bảng xem trước.</summary>
public sealed record ImportRowOutcome(
    int RowNumber,
    ImportRowAction Action,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string> Data);

/// <summary>Kết quả phân tích cả tệp; giữ dữ liệu có kiểu bên trong để ghi.</summary>
public abstract class ImportAnalysis
{
    /// <summary>Kết quả từng dòng.</summary>
    public abstract IReadOnlyList<ImportRowOutcome> Rows { get; }

    /// <summary>Có ít nhất một dòng lỗi.</summary>
    public bool HasErrors => Rows.Any(r => r.Action == ImportRowAction.Error);
}

/// <summary>Cầu nối không generic để khung import gọi một <see cref="IImportDefinition{TRow}"/> bất kỳ.</summary>
public interface IImportProcessor
{
    /// <summary>Định nghĩa import.</summary>
    IImportDefinition Definition { get; }

    /// <summary>Kiểm tra cột bắt buộc, giá trị hợp lệ, gọi <c>ParseRow</c> rồi <c>ValidateAsync</c>.</summary>
    Task<ImportAnalysis> AnalyzeAsync(IReadOnlyList<ImportSourceRow> rows, CancellationToken ct);

    /// <summary>Gọi <c>CommitAsync</c> của định nghĩa với các dòng đã phân tích (phải không có lỗi).</summary>
    Task<ImportCommitResult> CommitAsync(ImportAnalysis analysis, CancellationToken ct);
}

/// <summary>Triển khai <see cref="IImportProcessor"/> cho một kiểu dòng.</summary>
public sealed class ImportProcessor<TRow> : IImportProcessor where TRow : class
{
    private readonly IImportDefinition<TRow> _definition;

    /// <summary>Khởi tạo bộ xử lý.</summary>
    public ImportProcessor(IImportDefinition<TRow> definition) => _definition = definition;

    /// <inheritdoc />
    public IImportDefinition Definition => _definition;

    /// <inheritdoc />
    public async Task<ImportAnalysis> AnalyzeAsync(IReadOnlyList<ImportSourceRow> rows, CancellationToken ct)
    {
        var typed = new List<ImportRow<TRow>>(rows.Count);
        foreach (var raw in rows)
        {
            var errors = new List<string>();
            var source = CheckColumns(raw, errors);
            var data = _definition.ParseRow(source, errors);
            var row = new ImportRow<TRow>(source, data);
            foreach (var error in errors)
                row.AddError(error);
            typed.Add(row);
        }

        await _definition.ValidateAsync(typed, ct);

        foreach (var row in typed.Where(r => r.HasErrors))
            row.Action = ImportRowAction.Error;
        foreach (var row in typed.Where(r => !r.HasErrors && r.Action == ImportRowAction.Error))
            row.AddError("Không xác định được thao tác cho dòng này. Hãy liên hệ quản trị hệ thống.");

        return new Analysis(typed);
    }

    /// <inheritdoc />
    public Task<ImportCommitResult> CommitAsync(ImportAnalysis analysis, CancellationToken ct)
    {
        if (analysis is not Analysis typed)
            throw new ArgumentException("Kết quả phân tích không thuộc loại import này.", nameof(analysis));
        if (typed.HasErrors)
            throw new InvalidOperationException("Không được ghi dữ liệu khi còn dòng lỗi.");

        return _definition.CommitAsync(typed.TypedRows, ct);
    }

    /// <summary>Kiểm tra cột bắt buộc và danh sách giá trị hợp lệ; chuẩn hóa giá trị về đúng cách viết trong danh sách.</summary>
    private ImportSourceRow CheckColumns(ImportSourceRow raw, List<string> errors)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var column in _definition.TemplateColumns)
        {
            var value = raw.Get(column.Key);
            if (value.Length == 0)
            {
                if (column.Required)
                    errors.Add($"Thiếu \"{column.Header}\". Hãy nhập giá trị cho cột này.");
            }
            else if (column.AllowedValues is { Count: > 0 } allowed)
            {
                var match = allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    errors.Add($"Giá trị \"{value}\" ở cột \"{column.Header}\" không hợp lệ. Chỉ nhận: {string.Join(", ", allowed)}.");
                else
                    value = match;
            }

            values[column.Key] = value;
        }

        return new ImportSourceRow(raw.RowNumber, values);
    }

    private sealed class Analysis : ImportAnalysis
    {
        public Analysis(List<ImportRow<TRow>> rows)
        {
            TypedRows = rows;
            Rows = rows
                .Select(r => new ImportRowOutcome(r.RowNumber, r.Action, r.Errors.ToList(), r.Source.Values))
                .ToList();
        }

        public List<ImportRow<TRow>> TypedRows { get; }

        public override IReadOnlyList<ImportRowOutcome> Rows { get; }
    }
}

/// <summary>Phiên xem trước: lưu dữ liệu thô của tệp để xác nhận sau (phân tích lại ngay trước khi ghi).</summary>
/// <param name="Id">Id phiên.</param>
/// <param name="Kind">Loại import.</param>
/// <param name="UserId">Người tải tệp lên — người duy nhất được xác nhận.</param>
/// <param name="FileName">Tên tệp.</param>
/// <param name="Rows">Dòng thô.</param>
/// <param name="ExpiresAt">Hết hạn (UTC).</param>
public sealed record ImportSession(
    Guid Id,
    string Kind,
    Guid UserId,
    string? FileName,
    IReadOnlyList<ImportSourceRow> Rows,
    DateTimeOffset ExpiresAt);

/// <summary>Lưu phiên xem trước (bộ nhớ, hết hạn sau <see cref="ImportLimits.SessionLifetime"/>).</summary>
public interface IImportSessionStore
{
    /// <summary>Tạo phiên mới.</summary>
    ImportSession Create(string kind, Guid userId, string? fileName, IReadOnlyList<ImportSourceRow> rows);

    /// <summary>Lấy và <b>xóa</b> phiên (mỗi phiên chỉ xác nhận một lần); null nếu không có hoặc đã hết hạn.</summary>
    ImportSession? Take(Guid id);

    /// <summary>Xem phiên mà không xóa; null nếu không có hoặc đã hết hạn.</summary>
    ImportSession? Peek(Guid id);
}

/// <summary>Tệp kết quả đã lưu.</summary>
public sealed record StoredImportResult(string FileName, byte[] Content);

/// <summary>
/// Lưu tệp kết quả (có thể chứa mật khẩu tạm) <b>chỉ trong bộ nhớ</b>: tải được một lần,
/// hết hạn sau <see cref="ImportLimits.SessionLifetime"/>, chỉ người tạo tải được.
/// </summary>
public interface IImportResultStore
{
    /// <summary>Lưu tệp, trả mã tải và thời điểm hết hạn.</summary>
    (string Token, DateTimeOffset ExpiresAt) Add(Guid userId, string fileName, byte[] content);

    /// <summary>Lấy và xóa tệp; null nếu mã sai, đã tải, đã hết hạn hoặc không phải của người này.</summary>
    StoredImportResult? Take(string token, Guid userId);
}
