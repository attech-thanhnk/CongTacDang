using System.Collections.Generic;
using System.Globalization;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Báo cáo kết quả tập thể / lĩnh vực phục vụ liên kết trách nhiệm cán bộ lãnh đạo.</summary>
public sealed class Mau06Data
{
    public const string TemplateFileName = "Mau_06_BaoCaoKetQuaTapThe.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }

    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Lần cập nhật hồ sơ tập thể gần nhất (chưa sửa → ngày tạo).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("QUARTER")] public string? Quarter { get; init; }

    [TemplateField("YEAR")] public string? Year { get; init; }

    [TemplateField("SUBJECT_NAME")] public string? SubjectName { get; init; }

    [TemplateField("HEAD_NAME")] public string? HeadName { get; init; }

    [TemplateField("HEAD_TITLE")] public string? HeadTitle { get; init; }

    /// <summary>Số nhiệm vụ nhập trên hồ sơ; chưa nhập → giữ dòng chấm.</summary>
    [TemplateField("TASKS_ASSIGNED")] public string? TasksAssigned { get; init; }
    [TemplateField("TASKS_ONTIME")] public string? TasksOnTime { get; init; }
    [TemplateField("TASKS_LATE")] public string? TasksLate { get; init; }
    [TemplateField("TASKS_OBJECTIVE")] public string? TasksObjective { get; init; }

    /// <summary>Tỷ lệ trên tổng số nhiệm vụ được giao, một chữ số thập phân.</summary>
    [TemplateField("ONTIME_PERCENT")] public string? OnTimePercent { get; init; }
    [TemplateField("LATE_PERCENT")] public string? LatePercent { get; init; }

    [TemplateField("SAFETY")] public string? Safety { get; init; }
    [TemplateField("PARTY_BUILDING")] public string? PartyBuilding { get; init; }

    /// <summary>Điểm đã lưu trên hồ sơ, không tính lại.</summary>
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

    public static Mau06Data From(CollectiveEvaluationRecord record, PartyHeader header, IReadOnlyDictionary<string, string> sections)
    {
        string? Text(string code) => sections.TryGetValue(code, out var text) ? FormText.OrNull(text?.Trim()) : null;
        int? Count(string code) =>
            int.TryParse(Text(code), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        string? Number(int? n) => n?.ToString(CultureInfo.InvariantCulture);
        string? Percent(int? part, int? total) =>
            part.HasValue && total is > 0 ? FormText.Number(part.Value * 100.0 / total.Value, 1) : null;
        string Box(EvaluationGrade grade) => record.SelfProposedGrade == grade ? "☒" : "☐";

        var assigned = Count(Hd03FormCatalog.Form06AssignedCode);
        var onTime = Count(Hd03FormCatalog.Form06OnTimeCode);
        var late = Count(Hd03FormCatalog.Form06LateCode);
        return new Mau06Data
        {
            PartyParent = PartyHeader.Lines(header.Parent),
            PartyOrganization = PartyHeader.Lines(header.Organization),
            DateLine = CollectiveFormText.DateLine(record.UpdatedAt ?? record.CreatedAt),
            Quarter = CollectiveFormText.RomanQuarter(record.Period.Quarter),
            Year = FormText.Year(record.Period.Year),
            SubjectName = FormText.OrNull(record.SubjectName),
            HeadName = Text(Hd03FormCatalog.Form06HeadNameCode),
            HeadTitle = Text(Hd03FormCatalog.Form06HeadTitleCode),
            TasksAssigned = Number(assigned),
            TasksOnTime = Number(onTime),
            TasksLate = Number(late),
            TasksObjective = Number(Count(Hd03FormCatalog.Form06ObjectiveCode)),
            OnTimePercent = Percent(onTime, assigned),
            LatePercent = Percent(late, assigned),
            Safety = Text(Hd03FormCatalog.Form06SafetyCode),
            PartyBuilding = Text(Hd03FormCatalog.Form06PartyCode),
            TotalScore = CollectiveFormText.Score(record.TotalScore),
            CheckExcellent = Box(EvaluationGrade.HoanThanhXuatSac),
            CheckGood = Box(EvaluationGrade.HoanThanhTot),
            CheckDone = Box(EvaluationGrade.HoanThanh),
            CheckFailed = Box(EvaluationGrade.KhongHoanThanh)
        };
    }
}
