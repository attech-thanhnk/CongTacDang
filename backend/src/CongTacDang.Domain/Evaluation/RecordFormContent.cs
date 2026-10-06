using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Nội dung tự đánh giá và kết quả của một trục trong quý, nhập một lần trên phiếu tự chấm: danh sách nhiệm vụ (in ở mục I Mẫu 09C,
/// mỗi nhiệm vụ kèm mức tự đánh giá) và phần tóm tắt của trục (một dòng "Trục n" của Mẫu 9D, các cột (3)–(7) của biểu mẫu gốc).
/// </summary>
public sealed class AxisReport
{
    /// <summary>Các nhiệm vụ của trục (Mẫu 09C), theo thứ tự nhập.</summary>
    public List<AxisTask> Tasks { get; set; } = new();

    /// <summary>(3) Thời hạn hoàn thành (Mẫu 9D).</summary>
    public string? Deadline { get; set; }

    /// <summary>(4) Tình hình thực hiện (Mẫu 9D).</summary>
    public string? Status { get; set; }

    /// <summary>(5) Sản phẩm hoàn thành (Mẫu 9D).</summary>
    public string? Product { get; set; }

    /// <summary>(6) Đánh giá tiến độ (Mẫu 9D).</summary>
    public string? Progress { get; set; }

    /// <summary>(7) Ghi chú (Mẫu 9D).</summary>
    public string? Note { get; set; }
}

/// <summary>Một nhiệm vụ trong phần tự đánh giá theo trục của Mẫu 09C: "Nhiệm vụ n: nội dung. Tự đánh giá mức N."</summary>
public sealed class AxisTask
{
    /// <summary>Nội dung nhiệm vụ và tình hình, kết quả thực hiện.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Mức tự đánh giá (mã mức khai báo trong bộ tiêu chí); null = không kết luận mức.</summary>
    public int? Level { get; set; }

    /// <summary>Các ý con "+ …" của nhiệm vụ, mỗi ý có thể có mức riêng.</summary>
    public List<AxisTaskItem> Items { get; set; } = new();
}

/// <summary>Một ý con "+ …" của nhiệm vụ trong Mẫu 09C.</summary>
public sealed class AxisTaskItem
{
    /// <summary>Nội dung ý con.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Mức tự đánh giá của ý con; null = không kết luận mức.</summary>
    public int? Level { get; set; }
}

/// <summary>Khối một trục trong mục I của Mẫu 09C: tiêu đề "n. Tên trục" và các dòng nhiệm vụ / ý con đã dựng câu.</summary>
public sealed record AxisTaskBlock(string Title, IReadOnlyList<string> Lines);

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

//// <summary>
/// Đọc, kiểm tra, ghi nội dung biểu mẫu cá nhân lưu trên hồ sơ (jsonb): Mẫu 09C theo mã mục (<c>{ "I": "…" }</c> — phần nhận
/// xét chung), nhiệm vụ và kết quả theo trục dùng chung cho Mẫu 09C và 9D (<c>{ "T1": { "tasks": [ … ], "status": … } }</c>), ghi
/// chú theo trục của Mẫu 09B (<c>{ "T1": { "target": … } }</c>). Danh mục mục, trục, mức lấy từ bộ tiêu chí của kỳ nên đổi mẫu
/// không cần đổi schema.
/// </summary>
public static class RecordFormContent
{
    /// <summary>Số nhiệm vụ tối đa của một trục.</summary>
    public const int MaxTasksPerAxis = 30;

    /// <summary>Số ý con tối đa của một nhiệm vụ.</summary>
    public const int MaxItemsPerTask = 20;

    /// <summary>Độ dài tối đa nội dung một nhiệm vụ, một ý con, ô "Tình hình thực hiện", "Sản phẩm hoàn thành" (Mẫu 9D).</summary>
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
    /// buộc phải có nội dung. Mục đầu tiên in kèm các nhiệm vụ theo trục (<paramref name="axisReports"/>) nên số ký tự và điều kiện
    /// bắt buộc tính cả phần đó. Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateSelfAssessment(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? sections,
        IReadOnlyDictionary<string, AxisReport>? axisReports = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var input = sections ?? new Dictionary<string, string?>();
        foreach (var key in input.Keys)
        {
            if (!criteria.SelfAssessmentSections.Any(s => string.Equals(s.Code, key?.Trim(), StringComparison.OrdinalIgnoreCase)))
                return $"Mục \"{key}\" không có trong Mẫu 09C của bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.SelfAssessmentSections.Select(s => s.Code))}).";
        }
        var taskLength = axisReports == null ? 0 : AxisTaskTextLength(criteria, axisReports);
        for (var i = 0; i < criteria.SelfAssessmentSections.Count; i++)
        {
            var section = criteria.SelfAssessmentSections[i];
            var text = Find(input, section.Code)?.Trim() ?? string.Empty;
            var extra = i == 0 ? taskLength : 0;
            if (text.Length == 0 && extra == 0)
            {
                if (section.Required)
                    return $"Mẫu 09C: hãy nhập nội dung mục \"{section.Title}\".";
                continue;
            }
            var length = text.Length + extra;
            if (length > section.MaxLength)
                return $"Mẫu 09C: nội dung mục \"{section.Title}\"{(extra > 0 ? " (nhận xét chung và nhiệm vụ theo trục)" : string.Empty)} "
                    + $"dài {length} ký tự, vượt giới hạn {section.MaxLength} ký tự (biểu mẫu yêu cầu trình bày không quá 02 trang A4). "
                    + "Hãy rút gọn nội dung.";
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

    #region Nhiệm vụ và kết quả theo trục (Mẫu 09C mục I, Mẫu 9D)

    /// <summary>Đọc nội dung theo trục; chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, AxisReport> ParseAxisReports(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, AxisReport>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AxisReport>(
                JsonSerializer.Deserialize<Dictionary<string, AxisReport>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kiểm tra nội dung theo trục: mã trục có trong bộ tiêu chí; số nhiệm vụ, ý con; nhiệm vụ và ý con phải có nội dung; mức thuộc
    /// danh sách mức của bộ; độ dài từng ô. Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateAxisReports(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport?>? reports)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        foreach (var (code, report) in reports ?? new Dictionary<string, AxisReport?>())
        {
            var axis = criteria.FindAxis(code);
            if (axis == null)
                return $"Trục \"{code}\" không có trong bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.Axes.Select(a => a.Code))}).";
            if (report == null)
                continue;
            var axisLabel = $"Trục {criteria.Axes.IndexOf(axis) + 1}";
            var tasks = report.Tasks ?? new List<AxisTask>();
            if (tasks.Count > MaxTasksPerAxis)
                return $"{axisLabel}: tối đa {MaxTasksPerAxis} nhiệm vụ (đang có {tasks.Count}).";
            for (var k = 0; k < tasks.Count; k++)
            {
                var task = tasks[k];
                var label = $"{axisLabel}, nhiệm vụ {k + 1}";
                if (task == null)
                    return $"{label}: dữ liệu trống.";
                if (string.IsNullOrWhiteSpace(task.Content))
                    return $"{label}: hãy nhập nội dung (hoặc xóa nhiệm vụ).";
                if (task.Content.Trim().Length > MaxLongCellLength)
                    return $"{label}: nội dung không được dài quá {MaxLongCellLength} ký tự.";
                if (LevelError(criteria, task.Level, label) is { } levelError)
                    return levelError;
                var items = task.Items ?? new List<AxisTaskItem>();
                if (items.Count > MaxItemsPerTask)
                    return $"{label}: tối đa {MaxItemsPerTask} ý con (đang có {items.Count}).";
                for (var j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    var itemLabel = $"{label}, ý {j + 1}";
                    if (item == null || string.IsNullOrWhiteSpace(item.Content))
                        return $"{itemLabel}: hãy nhập nội dung (hoặc xóa ý).";
                    if (item.Content.Trim().Length > MaxLongCellLength)
                        return $"{itemLabel}: nội dung không được dài quá {MaxLongCellLength} ký tự.";
                    if (LevelError(criteria, item.Level, itemLabel) is { } itemLevelError)
                        return itemLevelError;
                }
            }
            foreach (var (name, value, max) in new[]
                     {
                         ("Tình hình thực hiện", report.Status, MaxLongCellLength),
                         ("Sản phẩm hoàn thành", report.Product, MaxLongCellLength),
                         ("Thời hạn hoàn thành", report.Deadline, MaxShortCellLength),
                         ("Đánh giá tiến độ", report.Progress, MaxShortCellLength),
                         ("Ghi chú", report.Note, MaxShortCellLength)
                     })
            {
                if ((value?.Trim().Length ?? 0) > max)
                    return $"{axisLabel}: ô \"{name}\" không được dài quá {max} ký tự.";
            }
        }
        return null;
    }

    /// <summary>
    /// Ghi nội dung theo trục đã kiểm tra: khóa theo mã trục của bộ (thứ tự trục), cắt khoảng trắng, bỏ trục không có nội dung.
    /// <paramref name="includeTasks"/> = kỳ áp dụng Mẫu 09C (lưu nhiệm vụ), <paramref name="includeSummary"/> = kỳ áp dụng Mẫu 9D
    /// (lưu các cột tóm tắt).
    /// </summary>
    public static string AxisReportsToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport?>? reports,
        bool includeTasks = true, bool includeSummary = true)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var input = reports ?? new Dictionary<string, AxisReport?>();
        var result = new Dictionary<string, AxisReport>();
        foreach (var axis in criteria.Axes)
        {
            var report = input.FirstOrDefault(kv => string.Equals(kv.Key?.Trim(), axis.Code, StringComparison.OrdinalIgnoreCase)).Value;
            if (report == null)
                continue;
            var clean = new AxisReport
            {
                Tasks = includeTasks
                    ? (report.Tasks ?? new List<AxisTask>()).Where(t => t != null && !string.IsNullOrWhiteSpace(t.Content)).Select(t => new AxisTask
                    {
                        Content = NormalizeNewLines(t.Content.Trim()),
                        Level = t.Level,
                        Items = (t.Items ?? new List<AxisTaskItem>()).Where(i => i != null && !string.IsNullOrWhiteSpace(i.Content))
                            .Select(i => new AxisTaskItem { Content = NormalizeNewLines(i.Content.Trim()), Level = i.Level }).ToList()
                    }).ToList()
                    : new List<AxisTask>(),
                Deadline = includeSummary ? Clean(report.Deadline) : null,
                Status = includeSummary ? Clean(report.Status) : null,
                Product = includeSummary ? Clean(report.Product) : null,
                Progress = includeSummary ? Clean(report.Progress) : null,
                Note = includeSummary ? Clean(report.Note) : null
            };
            if (clean.Tasks.Count > 0 || clean.Deadline != null || clean.Status != null || clean.Product != null
                || clean.Progress != null || clean.Note != null)
                result[axis.Code] = clean;
        }
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    /// <summary>
    /// Dựng phần tự đánh giá theo trục của mục I Mẫu 09C: mỗi trục có nhiệm vụ một khối "n. Tên trục", mỗi nhiệm vụ một dòng
    /// "&lt;tiền tố&gt; nội dung. &lt;câu nối mức&gt;" (tiền tố, câu nối theo bộ tiêu chí; chưa chọn mức thì bỏ câu nối), ý con là dòng
    /// "+ nội dung. &lt;câu nối mức&gt;" ngay dưới nhiệm vụ. Trục không có nhiệm vụ bị bỏ qua.
    /// </summary>
    public static List<AxisTaskBlock> BuildAxisTaskBlocks(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport> reports)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(reports);
        var prefix = string.IsNullOrWhiteSpace(criteria.TaskLineLabel) ? "{n}." : criteria.TaskLineLabel;
        var blocks = new List<AxisTaskBlock>();
        for (var i = 0; i < criteria.Axes.Count; i++)
        {
            var axis = criteria.Axes[i];
            var report = reports.FirstOrDefault(kv => string.Equals(kv.Key, axis.Code, StringComparison.OrdinalIgnoreCase)).Value;
            var tasks = (report?.Tasks ?? new List<AxisTask>()).Where(t => t != null && !string.IsNullOrWhiteSpace(t.Content)).ToList();
            if (tasks.Count == 0)
                continue;
            var lines = new List<string>();
            for (var k = 0; k < tasks.Count; k++)
            {
                var task = tasks[k];
                var number = (k + 1).ToString(CultureInfo.InvariantCulture);
                lines.Add($"{prefix.Replace("{n}", number)} {EndSentence(task.Content)}{LevelPhrase(criteria, task.Level)}");
                foreach (var item in (task.Items ?? new List<AxisTaskItem>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Content)))
                    lines.Add($"+ {EndSentence(item.Content)}{LevelPhrase(criteria, item.Level)}");
            }
            blocks.Add(new AxisTaskBlock($"{i + 1}. {axis.Name}", lines));
        }
        return blocks;
    }

    /// <summary>Số ký tự phần nhiệm vụ theo trục khi in ở mục I Mẫu 09C (tiêu đề trục và các dòng, mỗi dòng tính thêm một ký tự xuống dòng).</summary>
    public static int AxisTaskTextLength(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport> reports) =>
        BuildAxisTaskBlocks(criteria, reports).Sum(b => b.Title.Length + 1 + b.Lines.Sum(l => l.Length + 1));

    private static string? LevelError(CriteriaSetContent criteria, int? level, string label)
    {
        if (!level.HasValue || criteria.TaskLevels.Any(l => l.Code == level.Value))
            return null;
        return criteria.TaskLevels.Count == 0
            ? $"{label}: bộ tiêu chí của kỳ chưa khai báo mức tự đánh giá nhiệm vụ."
            : $"{label}: mức tự đánh giá chỉ nhận {string.Join(", ", criteria.TaskLevels.Select(l => l.Code))}.";
    }

    private static string LevelPhrase(CriteriaSetContent criteria, int? level) =>
        level.HasValue && criteria.TaskLevels.Any(l => l.Code == level.Value) && !string.IsNullOrWhiteSpace(criteria.TaskLevelPhrase)
            ? " " + criteria.TaskLevelPhrase.Replace("{level}", level.Value.ToString(CultureInfo.InvariantCulture))
            : string.Empty;

    private static string EndSentence(string value)
    {
        var t = NormalizeNewLines(value.Trim());
        return t.Length == 0 || ".!?;:".Contains(t[^1]) ? t : t + ".";
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
