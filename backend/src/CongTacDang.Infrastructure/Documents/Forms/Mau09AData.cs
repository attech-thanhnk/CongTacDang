using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 09A — Phiếu tự chấm điểm đánh giá, xếp loại chất lượng hằng quý (điểm nhiệm vụ từ Mẫu 02, áp dụng từ Quý I/2027).
/// Template: <c>Mau_09A_PhieuTuCham.docx</c> (dựng từ file biểu mẫu gốc). Nhóm tiêu chí chung lặp theo bộ tiêu chí của kỳ; điểm,
/// số sản phẩm vượt chuẩn, mức tự đề xuất lấy từ giá trị đã lưu.
/// </summary>
public sealed class Mau09AData
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_09A_PhieuTuCham.docx";

    private const string Checked = "☒";
    private const string Unchecked = "☐";

    /// <summary>Tên Chi bộ (in hoa) ở tiêu đề trái.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Chức vụ chính quyền; không có chức vụ nào trong danh mục thì chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    [TemplateCollection("GROUPS")] public List<GeneralGroupRow> Groups { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    /// <summary>Số sản phẩm "Ghi nhận vượt chuẩn" (đã lưu trên từng nhiệm vụ).</summary>
    [TemplateField("EXCEED_COUNT")] public string? ExceedCount { get; init; }

    /// <summary>Tổng số sản phẩm đăng ký.</summary>
    [TemplateField("TASK_COUNT")] public string? TaskCount { get; init; }

    /// <summary>Tỷ lệ vượt chuẩn (%, số nguyên; ký hiệu % in sẵn trong template).</summary>
    [TemplateField("EXCEED_PERCENT")] public string? ExceedPercent { get; init; }

    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu và bộ tiêu chí của kỳ.</summary>
    public static Mau09AData From(IndividualFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        var rounding = source.Criteria.Parameters.Rounding;
        var tasks = (record.Tasks ?? new List<Domain.Entities.EvaluationTask>()).Where(t => !t.IsDeleted).ToList();
        var exceed = tasks.Count(t => t.IsExceedStandard);
        var positions = source.Positions;
        var noPositions = positions.All() == null;
        string Box(EvaluationGrade grade) => record.SelfProposedGrade == grade ? Checked : Unchecked;

        return new Mau09AData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            PartyPosition = positions.Party,
            AdministrativePosition = noPositions ? FormText.OrNull(record.Member?.PositionTitle) : positions.Administrative,
            MassPosition = positions.MassOrganization,
            Department = FormText.OrNull(record.Department?.Name),
            Groups = Mau09Shared.GeneralGroups(source),
            GeneralScore = Mau09Shared.Score(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.Score(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.Score(source, record.TotalSelfScore, rounding.Total),
            ExceedCount = tasks.Count > 0 ? exceed.ToString(CultureInfo.InvariantCulture) : null,
            TaskCount = tasks.Count > 0 ? tasks.Count.ToString(CultureInfo.InvariantCulture) : null,
            ExceedPercent = tasks.Count > 0 ? Math.Round(100.0 * exceed / tasks.Count).ToString("0", CultureInfo.InvariantCulture) : null,
            CheckExcellent = Box(EvaluationGrade.HoanThanhXuatSac),
            CheckGood = Box(EvaluationGrade.HoanThanhTot),
            CheckDone = Box(EvaluationGrade.HoanThanh),
            CheckFailed = Box(EvaluationGrade.KhongHoanThanh)
        };
    }
}
