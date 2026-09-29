using System;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 17 — Kế hoạch hỗ trợ, khắc phục và phát triển 30-60-90 ngày (task 20 — T-88).
/// Template: <c>Mau_17_KeHoachHoTro.docx</c> — dựng từ đúng phần Mẫu 17 của biểu mẫu gốc HD03 (bố cục, chữ in sẵn giữ nguyên).
/// Mọi giá trị lấy từ kế hoạch và hồ sơ đã lưu; ô chưa nhập giữ chữ mặc định (dòng chấm) trong template.
/// </summary>
public sealed class Mau17Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_17_KeHoachHoTro.docx";

    /// <summary>Ô đã đánh dấu (thay ký hiệu ☐ in sẵn).</summary>
    public const string CheckedBox = "☒";

    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>"Mức xếp loại quý vừa qua" — mức chính thức kèm ký hiệu Mức A–D (chữ in trên Mẫu 17: "Mức C / Mức D").</summary>
    [TemplateField("GRADE_LEVEL")] public string? GradeLevel { get; init; }

    [TemplateField("SUPPORTER_NAME")] public string? SupporterName { get; init; }
    [TemplateField("SUPPORTER_TITLE")] public string? SupporterTitle { get; init; }

    [TemplateField("M30_LIMITATION")] public string? M30Limitation { get; init; }
    [TemplateField("M30_TARGET")] public string? M30Target { get; init; }
    [TemplateField("M30_MEASURES")] public string? M30Measures { get; init; }
    [TemplateField("M30_COORDINATION")] public string? M30Coordination { get; init; }
    [TemplateField("M30_ACHIEVED_BOX")] public string? M30AchievedBox { get; init; }
    [TemplateField("M30_NOT_ACHIEVED_BOX")] public string? M30NotAchievedBox { get; init; }

    [TemplateField("M60_LIMITATION")] public string? M60Limitation { get; init; }
    [TemplateField("M60_TARGET")] public string? M60Target { get; init; }
    [TemplateField("M60_MEASURES")] public string? M60Measures { get; init; }
    [TemplateField("M60_COORDINATION")] public string? M60Coordination { get; init; }
    [TemplateField("M60_ACHIEVED_BOX")] public string? M60AchievedBox { get; init; }
    [TemplateField("M60_NOT_ACHIEVED_BOX")] public string? M60NotAchievedBox { get; init; }

    [TemplateField("M90_LIMITATION")] public string? M90Limitation { get; init; }
    [TemplateField("M90_TARGET")] public string? M90Target { get; init; }
    [TemplateField("M90_MEASURES")] public string? M90Measures { get; init; }
    [TemplateField("M90_COORDINATION")] public string? M90Coordination { get; init; }
    [TemplateField("M90_ACHIEVED_BOX")] public string? M90AchievedBox { get; init; }
    [TemplateField("M90_NOT_ACHIEVED_BOX")] public string? M90NotAchievedBox { get; init; }

    /// <summary>Họ tên thủ trưởng đơn vị đã duyệt (dưới chữ ký).</summary>
    [TemplateField("APPROVER_NAME")] public string? ApproverName { get; init; }

    /// <summary>Họ tên người trực tiếp hỗ trợ, theo dõi (dưới chữ ký).</summary>
    [TemplateField("SUPPORTER_SIGN_NAME")] public string? SupporterSignName { get; init; }

    /// <summary>Họ tên cá nhân cam kết khắc phục (dưới chữ ký).</summary>
    [TemplateField("MEMBER_SIGN_NAME")] public string? MemberSignName { get; init; }

    /// <summary>Ký hiệu mức theo HD03 (A: Hoàn thành xuất sắc … D: Không hoàn thành).</summary>
    public static string? GradeLevelText(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => $"{FormText.Grade(grade)} - Mức A",
        EvaluationGrade.HoanThanhTot => $"{FormText.Grade(grade)} - Mức B",
        EvaluationGrade.HoanThanh => $"{FormText.Grade(grade)} - Mức C",
        EvaluationGrade.KhongHoanThanh => $"{FormText.Grade(grade)} - Mức D",
        _ => null
    };

    /// <summary>Dựng dữ liệu từ kế hoạch và hồ sơ (nạp kèm Member, Department, PartyCell).</summary>
    public static Mau17Data From(ImprovementPlan plan, EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(record);
        var content = ImprovementPlanContent.Parse(plan.Content);
        var m30 = content.Stage(ImprovementPlanContent.M30);
        var m60 = content.Stage(ImprovementPlanContent.M60);
        var m90 = content.Stage(ImprovementPlanContent.M90);
        static string? Box(ImprovementMilestone stage, string result) => stage.Result == result ? CheckedBox : null;

        return new Mau17Data
        {
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            GradeLevel = GradeLevelText(record.FinalGrade),
            SupporterName = content.SupporterName,
            SupporterTitle = content.SupporterTitle,
            M30Limitation = m30.Limitation,
            M30Target = m30.Target,
            M30Measures = m30.Measures,
            M30Coordination = m30.Coordination,
            M30AchievedBox = Box(m30, ImprovementPlanContent.ResultAchieved),
            M30NotAchievedBox = Box(m30, ImprovementPlanContent.ResultNotAchieved),
            M60Limitation = m60.Limitation,
            M60Target = m60.Target,
            M60Measures = m60.Measures,
            M60Coordination = m60.Coordination,
            M60AchievedBox = Box(m60, ImprovementPlanContent.ResultAchieved),
            M60NotAchievedBox = Box(m60, ImprovementPlanContent.ResultNotAchieved),
            M90Limitation = m90.Limitation,
            M90Target = m90.Target,
            M90Measures = m90.Measures,
            M90Coordination = m90.Coordination,
            M90AchievedBox = Box(m90, ImprovementPlanContent.ResultAchieved),
            M90NotAchievedBox = Box(m90, ImprovementPlanContent.ResultNotAchieved),
            ApproverName = FormText.OrNull(plan.ApprovedByName),
            SupporterSignName = content.SupporterName,
            MemberSignName = plan.AcknowledgedAt.HasValue ? FormText.OrNull(record.Member?.FullName) : null
        };
    }
}
