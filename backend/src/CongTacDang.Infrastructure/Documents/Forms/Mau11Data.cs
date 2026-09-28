using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 11 — Phiếu đánh giá, xếp loại cán bộ quý (bỏ phiếu kín).
/// Template: <c>Mau_11_PhieuBoPhieuChiBo.docx</c>.
/// </summary>
public sealed class Mau11Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_11_PhieuBoPhieuChiBo.docx";

    /// <summary>Tên Chi bộ (chữ hoa); xuất toàn Đảng bộ thì giữ chữ mặc định trong template.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    [TemplateField("PERIOD_QUARTER_YEAR")] public string? PeriodQuarterYear { get; init; }

    [TemplateCollection("RECORDS")] public List<Mau11Row> Records { get; init; } = new();

    /// <summary>Dựng dữ liệu mẫu từ kỳ và các hồ sơ đã lưu (nạp kèm Member, Department, PartyCell).</summary>
    public static Mau11Data From(EvaluationPeriod period, IEnumerable<EvaluationRecord> records, string? branchName)
    {
        return new Mau11Data
        {
            PartyCell = FormText.OrNull(branchName)?.ToUpperInvariant(),
            PeriodQuarterYear = $"QUÝ {FormText.Quarter(period.Quarter)} NĂM {FormText.Year(period.Year)}",
            Records = records.Select((r, index) => new Mau11Row
            {
                Order = (index + 1).ToString(),
                Name = FormText.OrNull(r.Member?.FullName),
                PositionAndDepartment = $"{r.Member?.PositionTitle} • {r.Department?.Name ?? r.PartyCell?.Name}",
                GeneralScore = FormText.Number(r.GeneralCriteriaScore, 1),
                TasksScore = FormText.Number(r.TasksScore, 1),
                SelfGrade = FormText.Grade(r.SelfProposedGrade)
            }).ToList()
        };
    }
}

/// <summary>Một dòng cán bộ của Mẫu 11.</summary>
public sealed class Mau11Row
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_NAME")] public string? Name { get; init; }
    [TemplateField("R_POSITION_DEPT")] public string? PositionAndDepartment { get; init; }

    /// <summary>Cột (4a) Điểm nhóm tiêu chí chung — giá trị đã lưu.</summary>
    [TemplateField("R_GENERAL_SCORE")] public string? GeneralScore { get; init; }

    /// <summary>Cột (4b) Điểm nhóm tiêu chí kết quả thực hiện nhiệm vụ — giá trị đã lưu.</summary>
    [TemplateField("R_TASKS_SCORE")] public string? TasksScore { get; init; }

    /// <summary>Cột (4c) Mức tự xếp loại đề xuất.</summary>
    [TemplateField("R_SELF_GRADE")] public string? SelfGrade { get; init; }
}
