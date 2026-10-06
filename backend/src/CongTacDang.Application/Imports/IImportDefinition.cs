using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;

namespace CongTacDang.Application.Imports;

/// <summary>
/// Định nghĩa một loại nhập dữ liệu từ Excel. Đăng ký qua DI (<c>services.AddImportDefinition&lt;T&gt;()</c>); controller và
/// trang nhập dữ liệu tự hiện loại mới, không cần sửa.
/// </summary>
public interface IImportDefinition
{
    /// <summary>Mã loại (dùng trong đường dẫn API), ví dụ <c>departments</c>.</summary>
    string Kind { get; }

    /// <summary>Tên hiển thị.</summary>
    string Name { get; }

    /// <summary>Mô tả ngắn.</summary>
    string Description { get; }

    /// <summary>Thứ tự hiển thị (nhỏ trước) — theo thứ tự nhập khuyến nghị.</summary>
    int Order { get; }

    /// <summary>Mã quyền dữ liệu cần có (ở phạm vi nào đó), ngoài <c>system.import</c>. Kiểm tra trên từng đối tượng do định nghĩa tự làm.</summary>
    IReadOnlyList<string> RequiredPermissions { get; }

    /// <summary>Cấu trúc cột (dùng để đọc file).</summary>
    IReadOnlyList<ImportColumn> Columns { get; }

    /// <summary>Cột kèm danh sách giá trị hợp lệ lấy từ CSDL (dùng để dựng file mẫu).</summary>
    Task<IReadOnlyList<ImportColumn>> GetTemplateColumnsAsync(CancellationToken ct = default);

    /// <summary>Phân tích từng dòng và kiểm tra chéo (trùng trong file, tham chiếu CSDL, quyền trên từng đối tượng).</summary>
    Task<ImportAnalysis> AnalyzeAsync(IReadOnlyList<ImportRawRow> rows, CancellationToken ct = default);

    /// <summary>Ghi dữ liệu đã phân tích (không có lỗi). Được gọi bên trong transaction của <c>IUnitOfWork</c>.</summary>
    Task<ImportCommitOutcome> CommitAsync(ImportAnalysis analysis, CancellationToken ct = default);
}

/// <summary>Một dòng đã đọc được dữ liệu kiểu <typeparamref name="TRow"/>.</summary>
public sealed class ParsedImportRow<TRow> where TRow : class
{
    /// <summary>Khởi tạo.</summary>
    public ParsedImportRow(ImportRowResult result, TRow data)
    {
        Result = result;
        Data = data;
    }

    /// <summary>Kết quả của dòng (hành động, lỗi).</summary>
    public ImportRowResult Result { get; }

    /// <summary>Dữ liệu đã đọc.</summary>
    public TRow Data { get; }

    /// <summary>Số dòng trong file.</summary>
    public int RowNumber => Result.RowNumber;

    /// <summary>Dòng chưa có lỗi.</summary>
    public bool IsValid => Result.IsValid;

    /// <summary>Thêm lỗi.</summary>
    public void Error(string message) => Result.AddError(message);
}

/// <summary>
/// Khung chung cho một loại nhập: đọc từng dòng (<see cref="ParseRow"/>) → kiểm tra chéo (<see cref="ValidateAsync"/>) →
/// ghi (<see cref="CommitRowsAsync"/>). Lỗi nghiệp vụ khi ghi một dòng được gắn số dòng.
/// </summary>
public abstract class ImportDefinition<TRow> : IImportDefinition where TRow : class
{
    /// <inheritdoc />
    public abstract string Kind { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> RequiredPermissions { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<ImportColumn> Columns { get; }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ImportColumn>> GetTemplateColumnsAsync(CancellationToken ct = default)
        => Task.FromResult(Columns);

    /// <summary>
    /// Đọc một dòng: kiểm tra cột bắt buộc, định dạng. Ghi lỗi vào <paramref name="result"/>; trả null nếu không đọc được
    /// (dòng sẽ không tham gia kiểm tra chéo).
    /// </summary>
    protected abstract TRow? ParseRow(ImportRawRow raw, ImportRowResult result);

    /// <summary>Kiểm tra chéo các dòng đọc được; đặt <see cref="ImportRowResult.Action"/> (Create/Update) và lỗi.</summary>
    protected abstract Task ValidateAsync(IReadOnlyList<ParsedImportRow<TRow>> rows, CancellationToken ct);

    /// <summary>Ghi các dòng (đều hợp lệ) theo thứ tự trong file.</summary>
    protected abstract Task<ImportCommitOutcome> CommitRowsAsync(IReadOnlyList<ParsedImportRow<TRow>> rows, CancellationToken ct);

    /// <inheritdoc />
    public async Task<ImportAnalysis> AnalyzeAsync(IReadOnlyList<ImportRawRow> rows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var results = new List<ImportRowResult>(rows.Count);
        var parsed = new List<ParsedImportRow<TRow>>(rows.Count);
        foreach (var raw in rows)
        {
            var result = new ImportRowResult(raw);
            results.Add(result);
            foreach (var column in Columns.Where(c => c.Required && raw.Get(c.Key).Length == 0))
                result.AddError($"Thiếu \"{column.Header}\".");
            foreach (var column in Columns.Where(c => raw.Get(c.Key).Length > ImportLimits.MaxCellLength))
                result.AddError($"\"{column.Header}\" dài quá {ImportLimits.MaxCellLength} ký tự.");

            var data = ParseRow(raw, result);
            if (data != null)
                parsed.Add(new ParsedImportRow<TRow>(result, data));
        }

        await ValidateAsync(parsed, ct);

        foreach (var result in results.Where(r => !r.IsValid))
            result.Action = ImportRowAction.Error;
        return new ImportAnalysis(results, parsed);
    }

    /// <inheritdoc />
    public Task<ImportCommitOutcome> CommitAsync(ImportAnalysis analysis, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (analysis.HasErrors || analysis.Payload is not List<ParsedImportRow<TRow>> parsed)
            throw new ValidationException("Dữ liệu còn dòng lỗi nên chưa nhập. Hãy sửa file rồi tải lên lại.");
        return CommitRowsAsync(parsed, ct);
    }

    /// <summary>Chạy thao tác ghi của một dòng; lỗi nghiệp vụ được gắn số dòng.</summary>
    protected static async Task<T> RunRowAsync<T>(int rowNumber, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AppException ex) when (ex is not ImportRowException)
        {
            throw new ImportRowException(rowNumber, ex);
        }
    }

    /// <summary>Chạy thao tác ghi của một dòng (không trả giá trị).</summary>
    protected static Task RunRowAsync(int rowNumber, Func<Task> action)
        => RunRowAsync(rowNumber, async () =>
        {
            await action();
            return true;
        });

    /// <summary>Báo lỗi "trùng trong file" cho các dòng có cùng khóa (dòng sau trùng dòng trước).</summary>
    protected static void MarkDuplicates(IEnumerable<ParsedImportRow<TRow>> rows, Func<TRow, string?> key, string label)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var value = key(row.Data);
            if (string.IsNullOrEmpty(value))
                continue;
            if (seen.TryGetValue(value, out var first))
                row.Error($"{label} \"{value}\" trùng với dòng {first} trong file.");
            else
                seen[value] = row.RowNumber;
        }
    }
}
