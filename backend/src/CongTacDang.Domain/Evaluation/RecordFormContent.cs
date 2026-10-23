using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Nhiệm vụ của một trục (in ở Mẫu 09C, kèm mức) và phần tóm tắt (một dòng "Trục n" của Mẫu 9D, cột 3–7).</summary>
public sealed class AxisReport
{
    /// <summary>Mẫu 09C, theo thứ tự nhập.</summary>
    public List<AxisTask> Tasks { get; set; } = new();

    /// <summary>Cột (3) Mẫu 9D.</summary>
    public string? Deadline { get; set; }

    /// <summary>Cột (4) Mẫu 9D.</summary>
    public string? Status { get; set; }

    /// <summary>Cột (5) Mẫu 9D.</summary>
    public string? Product { get; set; }

    /// <summary>Cột (6) Mẫu 9D.</summary>
    public string? Progress { get; set; }

    /// <summary>Cột (7) Mẫu 9D.</summary>
    public string? Note { get; set; }
}

/// <summary>In ở Mẫu 09C: "Nhiệm vụ n: nội dung. Tự đánh giá mức N."</summary>
public sealed class AxisTask
{
    public string Content { get; set; } = string.Empty;

    /// <summary>Mã mức khai báo trong bộ tiêu chí; null = không kết luận mức.</summary>
    public int? Level { get; set; }

    /// <summary>Các ý con "+ …", mỗi ý có thể có mức riêng.</summary>
    public List<AxisTaskItem> Items { get; set; } = new();
}

public sealed class AxisTaskItem
{
    public string Content { get; set; } = string.Empty;

    /// <summary>Null = không kết luận mức.</summary>
    public int? Level { get; set; }
}

/// <summary>Khối một trục ở Mẫu 09C: tiêu đề "n. Tên trục (trục n)" và các dòng đã dựng câu.</summary>
public sealed record AxisTaskBlock(string Title, IReadOnlyList<AxisTaskLine> Lines);

/// <summary>Dòng nhiệm vụ / ý con; <c>Level</c> là câu nối mức, in nghiêng ngay sau nội dung (null khi không kết luận mức).</summary>
public sealed record AxisTaskLine(string Text, string? Level)
{
    public string FullText => Level == null ? Text : Text + " " + Level;
}

/// <summary>Các ô tự luận của một trục trên Mẫu 09B; điểm trục lưu riêng ở <c>EvaluationRecord.AxisScores</c>.</summary>
public sealed class AxisNote
{
    public string? Target { get; set; }

    public string? Result { get; set; }

    public string? Note { get; set; }
}

/// <summary>Đọc, kiểm tra, ghi nội dung biểu mẫu cá nhân (jsonb) theo mục, trục, mức của bộ tiêu chí của kỳ.</summary>
public static class RecordFormContent
{
    public const int MaxTasksPerAxis = 30;

    public const int MaxItemsPerTask = 20;

    /// <summary>Nhiệm vụ, ý con, ô "Tình hình thực hiện", "Sản phẩm hoàn thành" (Mẫu 9D).</summary>
    public const int MaxLongCellLength = 2000;

    /// <summary>Ô "Thời hạn", "Đánh giá tiến độ", "Ghi chú" (Mẫu 9D).</summary>
    public const int MaxShortCellLength = 500;

    /// <summary>Mỗi ô tự luận của một trục trên Mẫu 09B.</summary>
    public const int MaxAxisNoteLength = 4000;

    public const int MaxGroupNoteLength = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Mẫu 09C — tự đánh giá theo mục

    /// <summary>Chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, string> ParseSelfAssessment(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Mục đầu tiên in kèm nhiệm vụ theo trục nên số ký tự và điều kiện bắt buộc tính cả phần đó.
    /// <paramref name="partial"/> = lưu nháp, mục bắt buộc được để trống. Trả lỗi hoặc null.</summary>
    public static string? ValidateSelfAssessment(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? sections,
        IReadOnlyDictionary<string, AxisReport>? axisReports = null, bool partial = false)
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
                if (section.Required && !partial)
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

    /// <summary>Khóa theo mã mục của bộ, bỏ mục rỗng.</summary>
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

    /// <summary>Chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, AxisReport> ParseAxisReports(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, AxisReport>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AxisReport>(
                JsonSerializer.Deserialize<Dictionary<string, AxisReport>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Trả lỗi hoặc null. <paramref name="partial"/> = lưu nháp: dòng chưa có nội dung không báo lỗi
    /// (khi ghi sẽ bị bỏ).</summary>
    public static string? ValidateAxisReports(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport?>? reports,
        bool partial = false)
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
                if (task == null || string.IsNullOrWhiteSpace(task.Content))
                {
                    if (partial)
                        continue;
                    return task == null ? $"{label}: dữ liệu trống." : $"{label}: hãy nhập nội dung (hoặc xóa nhiệm vụ).";
                }
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
                    {
                        if (partial)
                            continue;
                        return $"{itemLabel}: hãy nhập nội dung (hoặc xóa ý).";
                    }
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

    /// <summary><paramref name="includeTasks"/> = kỳ áp dụng 09C, <paramref name="includeSummary"/> = kỳ áp dụng 9D.
    /// Bỏ trục không có nội dung.</summary>
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

    /// <summary>Câu nối mức tách riêng để in nghiêng; trục không có nhiệm vụ bị bỏ qua.</summary>
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
            var lines = new List<AxisTaskLine>();
            for (var k = 0; k < tasks.Count; k++)
            {
                var task = tasks[k];
                var number = (k + 1).ToString(CultureInfo.InvariantCulture);
                lines.Add(new AxisTaskLine($"{prefix.Replace("{n}", number)} {EndSentence(task.Content)}", LevelPhrase(criteria, task.Level)));
                foreach (var item in (task.Items ?? new List<AxisTaskItem>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Content)))
                    lines.Add(new AxisTaskLine($"+ {EndSentence(item.Content)}", LevelPhrase(criteria, item.Level)));
            }
            var axisNumber = (i + 1).ToString(CultureInfo.InvariantCulture);
            blocks.Add(new AxisTaskBlock($"{axisNumber}. {axis.Name} (trục {axisNumber})", lines));
        }
        return blocks;
    }

    /// <summary>Mỗi dòng tính thêm một ký tự xuống dòng.</summary>
    public static int AxisTaskTextLength(CriteriaSetContent criteria, IReadOnlyDictionary<string, AxisReport> reports) =>
        BuildAxisTaskBlocks(criteria, reports).Sum(b => b.Title.Length + 1 + b.Lines.Sum(l => l.FullText.Length + 1));

    private static string? LevelError(CriteriaSetContent criteria, int? level, string label)
    {
        if (!level.HasValue || criteria.TaskLevels.Any(l => l.Code == level.Value))
            return null;
        return criteria.TaskLevels.Count == 0
            ? $"{label}: bộ tiêu chí của kỳ chưa khai báo mức tự đánh giá nhiệm vụ."
            : $"{label}: mức tự đánh giá chỉ nhận {string.Join(", ", criteria.TaskLevels.Select(l => l.Code))}.";
    }

    private static string? LevelPhrase(CriteriaSetContent criteria, int? level) =>
        level.HasValue && criteria.TaskLevels.Any(l => l.Code == level.Value) && !string.IsNullOrWhiteSpace(criteria.TaskLevelPhrase)
            ? criteria.TaskLevelPhrase.Trim().Replace("{level}", level.Value.ToString(CultureInfo.InvariantCulture))
            : null;

    private static string EndSentence(string value)
    {
        var t = NormalizeNewLines(value.Trim());
        return t.Length == 0 || ".!?;:".Contains(t[^1]) ? t : t + ".";
    }

    #endregion

    #region Nhận xét theo nhóm tiêu chí chung (Mẫu 09A/09B)

    /// <summary>Chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, string> ParseGeneralGroupNotes(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Trả lỗi hoặc null.</summary>
    public static string? ValidateGeneralGroupNotes(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? notes)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        foreach (var (code, text) in notes ?? new Dictionary<string, string?>())
        {
            var group = criteria.GeneralGroups.FirstOrDefault(g => string.Equals(g.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (group == null)
                return $"Nhóm tiêu chí \"{code}\" không có trong bộ tiêu chí của kỳ (có: {string.Join(", ", criteria.GeneralGroups.Select(g => g.Code))}).";
            if ((text?.Trim().Length ?? 0) > MaxGroupNoteLength)
                return $"Nhận xét nhóm tiêu chí {group.Code} không được dài quá {MaxGroupNoteLength} ký tự.";
        }
        return null;
    }

    /// <summary>Bỏ nhóm trống; không có gì → null.</summary>
    public static string? GeneralGroupNotesToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, string?>? notes)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var input = notes ?? new Dictionary<string, string?>();
        var result = new Dictionary<string, string>();
        foreach (var group in criteria.GeneralGroups)
        {
            var text = Clean(Find(input, group.Code));
            if (text != null)
                result[group.Code] = text;
        }
        return result.Count == 0 ? null : JsonSerializer.Serialize(result, JsonOptions);
    }

    #endregion

    #region Mẫu 09B — nội dung tự luận theo trục

    /// <summary>Chuỗi rỗng → rỗng; JSON sai → <see cref="JsonException"/>.</summary>
    public static Dictionary<string, AxisNote> ParseAxisNotes(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, AxisNote>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, AxisNote>(
                JsonSerializer.Deserialize<Dictionary<string, AxisNote>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Trả lỗi hoặc null.</summary>
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

    /// <summary>Bỏ trục không có nội dung; không có gì → null.</summary>
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
