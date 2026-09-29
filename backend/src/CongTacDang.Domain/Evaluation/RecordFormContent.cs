using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Một dòng của Mẫu 9D "Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý" — các cột (2)–(7) của biểu mẫu gốc,
/// nhóm theo trục (mã trục thuộc bộ tiêu chí của kỳ).
/// </summary>
public sealed class TaskResultRow
{
    /// <summary>Mã trục (T1…) trong bộ tiêu chí của kỳ.</summary>
    public string AxisCode { get; set; } = string.Empty;

    /// <summary>(2) Nội dung nhiệm vụ, công việc.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>(3) Thời hạn hoàn thành (ghi như trên biểu mẫu, ví dụ "30/9/2026", "Quý III").</summary>
    public string? Deadline { get; set; }

    /// <summary>(4) Tình hình thực hiện.</summary>
    public string? Status { get; set; }

    /// <summary>(5) Sản phẩm hoàn thành.</summary>
    public string? Product { get; set; }

    /// <summary>(6) Đánh giá tiến độ.</summary>
    public string? Progress { get; set; }

    /// <summary>(7) Ghi chú.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Nội dung các cột tự luận của một trục trên Mẫu 09B (phần II): "Mục tiêu, nhiệm vụ đề ra", "Tóm tắt kết quả sản phẩm thực tế;
/// Tài liệu minh chứng", "Ghi chú". Điểm trục lưu riêng ở <c>EvaluationRecord.AxisScores</c>.
/// </summary>
public sealed class AxisNote
{
    /// <summary>Mục tiêu, nhiệm vụ đề ra.</summary>
    public string? Target { get; set; }

    /// <summary>Tóm tắt kết quả sản phẩm thực tế; tài liệu minh chứng.</summary>
    public string? Result { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Đọc, kiểm tra, ghi nội dung biểu mẫu cá nhân lưu trên hồ sơ (jsonb, task 18 — T-82): Mẫu 09C theo mã mục
/// (<c>{ "I": "…" }</c>), Mẫu 9D là danh sách dòng theo trục, ghi chú theo trục của Mẫu 09B (<c>{ "T1": { "target": … } }</c>).
/// Danh mục mục/trục lấy từ bộ tiêu chí của kỳ nên đổi mẫu không cần đổi schema.
/// </summary>
public static class RecordFormContent
{
    /// <summary>Số dòng tối đa của Mẫu 9D.</summary>
    public const int MaxTaskResultRows = 100;

    /// <summary>Độ dài tối đa ô "Nội dung nhiệm vụ", "Tình hình thực hiện", "Sản phẩm hoàn thành" (Mẫu 9D).</summary>
    public const int MaxLongCellLength = 2000;

    /// <summary>Độ dài tối đa ô "Thời hạn", "Đánh giá tiến độ", "Ghi chú" (Mẫu 9D).</summary>
    public const int MaxShortCellLength = 500;

    /// <summary>Độ dài tối đa mỗi ô tự luận của một trục trên Mẫu 09B.</summary>
    public const int MaxAxisNoteLength = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Mẫu 09C — tự đánh giá theo mục

    /// <summary>Đọc nội dung 09C; chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, string> ParseSelfAssessment(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kiểm tra nội dung 09C theo các mục của bộ tiêu chí: mã mục phải có trong bộ, không vượt số ký tự tối đa của mục, mục bắt
    /// buộc phải có nội dung. Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateSelfAssessment(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? sections)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var input = sections ?? new Dictionary<string, string?>();
        foreach (var key in input.Keys)
        {
            if (!criteria.SelfAssessmentSections.Any(s => string.Equals(s.Code, key?.Trim(), StringComparison.OrdinalIgnoreCase)))
                return $"Mục \"{key}\" không có trong Mẫu 09C của bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.SelfAssessmentSections.Select(s => s.Code))}).";
        }
        foreach (var section in criteria.SelfAssessmentSections)
        {
            var text = Find(input, section.Code)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (section.Required)
                    return $"Mẫu 09C: hãy nhập nội dung mục \"{section.Title}\".";
                continue;
            }
            if (text.Length > section.MaxLength)
                return $"Mẫu 09C: nội dung mục \"{section.Title}\" dài {text.Length} ký tự, vượt giới hạn {section.MaxLength} ký tự "
                    + "(biểu mẫu yêu cầu trình bày không quá 02 trang A4). Hãy rút gọn nội dung.";
        }
        return null;
    }

    /// <summary>Ghi nội dung 09C đã kiểm tra: khóa theo mã mục của bộ, cắt khoảng trắng, bỏ mục rỗng.</summary>
    public static string SelfAssessmentToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? sections)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var input = sections ?? new Dictionary<string, string?>();
        var result = new Dictionary<string, string>();
        foreach (var section in criteria.SelfAssessmentSections)
        {
            var text = Find(input, section.Code)?.Trim();
            if (!string.IsNullOrEmpty(text))
                result[section.Code] = NormalizeNewLines(text);
        }
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    #endregion

    #region Mẫu 9D — kết quả nhiệm vụ theo trục

    /// <summary>Đọc các dòng 9D; chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static List<TaskResultRow> ParseTaskResults(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new List<TaskResultRow>()
            : JsonSerializer.Deserialize<List<TaskResultRow>>(json, JsonOptions) ?? new List<TaskResultRow>();

    /// <summary>
    /// Kiểm tra các dòng 9D: tối đa <see cref="MaxTaskResultRows"/> dòng; mã trục có trong bộ tiêu chí; có nội dung nhiệm vụ; độ dài
    /// từng ô. Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateTaskResults(CriteriaSetContent criteria, IReadOnlyList<TaskResultRow?>? rows)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var list = rows ?? Array.Empty<TaskResultRow?>();
        if (list.Count > MaxTaskResultRows)
            return $"Mẫu 9D có tối đa {MaxTaskResultRows} dòng nhiệm vụ (đang có {list.Count}).";
        for (var i = 0; i < list.Count; i++)
        {
            var row = list[i];
            var label = $"Mẫu 9D, dòng {i + 1}";
            if (row == null)
                return $"{label}: dữ liệu trống.";
            if (criteria.FindAxis(row.AxisCode) == null)
                return $"{label}: trục \"{row.AxisCode}\" không có trong bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.Axes.Select(a => a.Code))}).";
            if (string.IsNullOrWhiteSpace(row.Content))
                return $"{label}: hãy nhập nội dung nhiệm vụ, công việc (hoặc xóa dòng).";
            foreach (var (name, value, max) in new[]
                     {
                         ("Nội dung nhiệm vụ, công việc", row.Content, MaxLongCellLength),
                         ("Tình hình thực hiện", row.Status, MaxLongCellLength),
                         ("Sản phẩm hoàn thành", row.Product, MaxLongCellLength),
                         ("Thời hạn hoàn thành", row.Deadline, MaxShortCellLength),
                         ("Đánh giá tiến độ", row.Progress, MaxShortCellLength),
                         ("Ghi chú", row.Note, MaxShortCellLength)
                     })
            {
                if ((value?.Trim().Length ?? 0) > max)
                    return $"{label}: ô \"{name}\" không được dài quá {max} ký tự.";
            }
        }
        return null;
    }

    /// <summary>Ghi các dòng 9D đã kiểm tra: mã trục theo bộ tiêu chí, cắt khoảng trắng, sắp theo thứ tự trục (giữ thứ tự nhập trong trục).</summary>
    public static string TaskResultsToJson(CriteriaSetContent criteria, IReadOnlyList<TaskResultRow?>? rows)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var axisOrder = criteria.Axes.Select((a, i) => (a.Code, i)).ToDictionary(x => x.Code, x => x.i, StringComparer.OrdinalIgnoreCase);
        var result = (rows ?? Array.Empty<TaskResultRow?>())
            .Where(r => r != null)
            .Select((r, index) => (Row: new TaskResultRow
            {
                AxisCode = criteria.FindAxis(r!.AxisCode)!.Code,
                Content = NormalizeNewLines(r.Content.Trim()),
                Deadline = Clean(r.Deadline),
                Status = Clean(r.Status),
                Product = Clean(r.Product),
                Progress = Clean(r.Progress),
                Note = Clean(r.Note)
            }, index))
            .OrderBy(x => axisOrder[x.Row.AxisCode])
            .ThenBy(x => x.index)
            .Select(x => x.Row)
            .ToList();
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    #endregion

    #region Mẫu 09B — nội dung tự luận theo trục

    /// <summary>Đọc ghi chú theo trục; chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, AxisNote> ParseAxisNotes(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, AxisNote>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AxisNote>(
                JsonSerializer.Deserialize<Dictionary<string, AxisNote>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Kiểm tra ghi chú theo trục (mã trục có trong bộ, độ dài từng ô). Trả thông báo lỗi hoặc null.</summary>
    public static string? ValidateAxisNotes(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisNote?>? notes)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        foreach (var (code, note) in notes ?? new Dictionary<string, AxisNote?>())
        {
            var axis = criteria.FindAxis(code);
            if (axis == null)
                return $"Trục \"{code}\" không có trong bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.Axes.Select(a => a.Code))}).";
            if (note == null)
                continue;
            foreach (var (name, value) in new[] { ("Mục tiêu, nhiệm vụ đề ra", note.Target), ("Tóm tắt kết quả; minh chứng", note.Result), ("Ghi chú", note.Note) })
            {
                if ((value?.Trim().Length ?? 0) > MaxAxisNoteLength)
                    return $"Trục {axis.Code}: ô \"{name}\" không được dài quá {MaxAxisNoteLength} ký tự.";
            }
        }
        return null;
    }

    /// <summary>Ghi ghi chú theo trục đã kiểm tra: khóa theo mã trục của bộ, bỏ trục không có nội dung; không có gì → null.</summary>
    public static string? AxisNotesToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisNote?>? notes)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var result = new Dictionary<string, AxisNote>();
        foreach (var axis in criteria.Axes)
        {
            var note = (notes ?? new Dictionary<string, AxisNote?>())
                .FirstOrDefault(kv => string.Equals(kv.Key?.Trim(), axis.Code, StringComparison.OrdinalIgnoreCase)).Value;
            if (note == null)
                continue;
            var clean = new AxisNote { Target = Clean(note.Target), Result = Clean(note.Result), Note = Clean(note.Note) };
            if (clean.Target != null || clean.Result != null || clean.Note != null)
                result[axis.Code] = clean;
        }
        return result.Count == 0 ? null : JsonSerializer.Serialize(result, JsonOptions);
    }

    #endregion

    private static string? Find(IReadOnlyDictionary<string, string?> input, string code) =>
        input.FirstOrDefault(kv => string.Equals(kv.Key?.Trim(), code, StringComparison.OrdinalIgnoreCase)).Value;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeNewLines(value.Trim());

    private static string NormalizeNewLines(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
}
