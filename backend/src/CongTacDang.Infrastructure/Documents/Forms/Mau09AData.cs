using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu tự chấm có điểm nhiệm vụ từ Mẫu 02; nhóm tiêu chí chung lặp theo bộ tiêu chí của kỳ.</summary>
public sealed class Mau09AData
{
    public const string TemplateFileName = "Mau_09A_PhieuTuCham.docx";

    private const string Checked = "☒";
    private const string Unchecked = "☐";

    /// <summary>In hoa.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>Ngày nộp phiếu tự chấm; chưa nộp → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Chỗ điền liền sau chữ "Quý" nên giá trị có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>dd/MM/yyyy; hồ sơ chưa có ngày sinh thì giữ dòng chấm.</summary>
    [TemplateField("DATE_OF_BIRTH")] public string? DateOfBirth { get; init; }
    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Không có chức vụ nào trong danh mục thì lấy chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    [TemplateCollection("GROUPS")] public List<GeneralGroupRow> Groups { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    [TemplateField("EXCEED_COUNT")] public string? ExceedCount { get; init; }

    [TemplateField("TASK_COUNT")] public string? TaskCount { get; init; }

    /// <summary>Số nguyên; ký hiệu % in sẵn trong template.</summary>
    [TemplateField("EXCEED_PERCENT")] public string? ExceedPercent { get; init; }

    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

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
            DateLine = Mau09Shared.DateLine(source),
            Quarter = FormText.Spaced(Mau09Shared.Quarter(record)),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            DateOfBirth = Mau09Shared.DateOfBirth(record),
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
