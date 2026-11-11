using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc.</summary>
public sealed class Mau02Data
{
    public const string TemplateFileName = "Mau_02_TuDanhGia.docx";

    /// <summary>Ngày nộp phiếu tự chấm; chưa nộp → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Cơ quan chủ quản trực tiếp của đơn vị hồ sơ (dòng 1 tiêu đề trái); null khi trực thuộc Công ty → in tên công ty.</summary>
    [TemplateField("ADMIN_PARENT_UPPER")] public string? AdminParentUpper { get; init; }

    [TemplateCondition("HAS_ADMIN_PARENT")] public bool HasAdminParent => AdminParentUpper != null;

    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Đơn vị gốc (chính là công ty) → tiêu đề trái chỉ in tên công ty, không lặp.</summary>
    [TemplateCondition("HAS_DEPARTMENT")] public bool HasDepartment { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }

    [TemplateCollection("TASKS")] public List<Mau02TaskRow> Tasks { get; init; } = new();

    /// <summary>Dòng "CỘNG" ("… / 70,0"); chưa có sản phẩm → giữ dòng chấm.</summary>
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    /// <summary>Dòng "CỘNG" ("… SP"); chưa có sản phẩm → giữ dòng chấm.</summary>
    [TemplateField("EXCEED_COUNT")] public string? ExceedCount { get; init; }

    /// <summary>Dòng "CỘNG": tổng trọng số các sản phẩm; chưa có sản phẩm → giữ chữ mặc định.</summary>
    [TemplateField("TOTAL_WEIGHT")] public string? TotalWeight { get; init; }

    // Ô ký: bước chưa làm hoặc do cấp trên thực hiện → giữ dòng chấm để ký tay (như Mẫu 01).

    /// <summary>Ô "Cá nhân tự đánh giá": chủ hồ sơ khi đã nộp phiếu tự chấm.</summary>
    [TemplateField("SELF_NAME")] public string? SelfName { get; init; }

    /// <summary>Ô "Cấp có thẩm quyền duyệt": người duyệt danh mục như Mẫu 01 (<see cref="Mau01Data.TaskApprover"/>).</summary>
    [TemplateField("APPROVER_NAME")] public string? ApproverName { get; init; }

    /// <summary>Ô "Đại diện lãnh đạo đơn vị": Trưởng đơn vị đã gửi danh mục (cùng người ký Mẫu 01).</summary>
    [TemplateField("UNIT_LEADER_NAME")] public string? UnitLeaderName { get; init; }

    /// <summary>
    /// <paramref name="evidenceNames"/>: tên tệp minh chứng (phiên bản hiện hành) theo Id nhiệm vụ;
    /// <paramref name="adminParentLine"/>: dòng 1 tiêu đề trái (<see cref="OrgHeaderLine.DirectParent"/>).
    /// </summary>
    public static Mau02Data From(EvaluationRecord record, IReadOnlyDictionary<Guid, string>? evidenceNames = null, string? adminParentLine = null)
    {
        var tasks = record.Tasks.OrderBy(t => t.TaskOrder).ToList();
        // Mặc định 2 chữ số nếu kỳ chưa có bộ tiêu chí.
        var scoreDecimals = FormCriteria.Read(record.Period)?.Content.Parameters.Rounding.TaskScore.Decimals ?? 2;
        return new Mau02Data
        {
            DateLine = CollectiveFormText.DateLine(record.SelfScoredAt),
            AdminParentUpper = FormText.OrNull(adminParentLine),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            HasDepartment = Mau09Shared.HasDepartment(record),
            TotalWeight = tasks.Count > 0 ? FormText.Number(tasks.Sum(t => t.Weight), 1) : null,
            SelfName = record.SelfScoredAt.HasValue ? FormText.OrNull(record.Member?.FullName) : null,
            ApproverName = Mau01Data.TaskApprover(record),
            UnitLeaderName = record.TasksForwardedById != null ? FormText.OrNull(record.TasksForwardedByName) : null,
            Quarter = record.Period != null ? CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            TotalScore = tasks.Count > 0 ? FormText.Number(tasks.Sum(t => t.SelfScore), scoreDecimals) : null,
            ExceedCount = tasks.Count > 0 ? tasks.Count(t => t.IsExceedStandard).ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            Tasks = tasks.Select((t, index) =>
            {
                // Điểm đạt = SelfScore đã lưu; Kết quả SP (%) chỉ là SelfScore / Trọng số.
                // Nhiệm vụ chưa nhập tỷ lệ A-B-C-D: để trống, không in thành 100%.
                var pending = TaskSelfScores.IsPending(t);
                double? resultPct = t.Weight > 0 ? Math.Round(t.SelfScore / t.Weight * 100, 1) : null;

                // Không có tệp minh chứng: in nguồn minh chứng đã đăng ký ở Mẫu 01, không có thì để trống.
                string? evidence = null;
                if (evidenceNames != null && evidenceNames.TryGetValue(t.Id, out var name))
                    evidence = FormText.OrNull(name);
                evidence ??= FormText.OrNull(t.EvidenceSource?.Trim()) ?? string.Empty;

                return new Mau02TaskRow
                {
                    Order = (index + 1).ToString(),
                    Name = t.TaskName,
                    Weight = FormText.Number(t.Weight, 1),
                    CriteriaA = pending ? null : FormText.Percent(t.CriteriaA_Ratio),
                    CriteriaB = pending ? null : FormText.Percent(t.CriteriaB_Ratio),
                    CriteriaC = pending ? null : FormText.Percent(t.CriteriaC_Ratio),
                    CriteriaD = pending ? null : FormText.Percent(t.CriteriaD_Ratio),
                    ResultPercent = resultPct.HasValue ? FormText.Number(resultPct.Value, 1) + "%" : "-",
                    Score = FormText.Number(t.SelfScore, scoreDecimals),
                    IsExceedStandard = t.IsExceedStandard,
                    Evidence = evidence
                };
            }).ToList()
        };
    }
}

/// <summary>Ảnh chụp bộ tiêu chí của kỳ; lỗi/không có → null.</summary>
internal static class FormCriteria
{
    public static CriteriaSnapshot? Read(EvaluationPeriod? period)
    {
        if (period == null)
            return null;
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

public sealed class Mau02TaskRow
{
    [TemplateField("T_STT")] public string? Order { get; init; }
    [TemplateField("T_NAME")] public string? Name { get; init; }
    [TemplateField("T_WEIGHT")] public string? Weight { get; init; }
    [TemplateField("T_A")] public string? CriteriaA { get; init; }
    [TemplateField("T_B")] public string? CriteriaB { get; init; }
    [TemplateField("T_C")] public string? CriteriaC { get; init; }
    [TemplateField("T_D")] public string? CriteriaD { get; init; }
    [TemplateField("T_RESULT_PCT")] public string? ResultPercent { get; init; }
    [TemplateField("T_SCORE")] public string? Score { get; init; }

    /// <summary>Chữ hiển thị nằm trong khối <c>if:T_IS_EXCEED</c> / <c>ifnot:T_IS_EXCEED</c> của template.</summary>
    [TemplateCondition("T_IS_EXCEED")] public bool IsExceedStandard { get; init; }

    /// <summary>Tên tệp minh chứng, không có thì nguồn minh chứng đã đăng ký; cả hai trống → ô trống.</summary>
    [TemplateField("T_EVIDENCE")] public string? Evidence { get; init; }
}
