using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu giao/đăng ký sản phẩm; trường null giữ chữ mặc định trong template.</summary>
public sealed class Mau01Data
{
    public const string TemplateFileName = "Mau_01_PhieuGiaoNhiemVu.docx";

    /// <summary>Ngày duyệt danh mục nhiệm vụ; chưa duyệt → giữ dòng chấm.</summary>
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

    // Bốn ô ký: người chưa thực hiện, bước tắt/bỏ qua hoặc cấp trên thực hiện → giữ dòng chấm để ký tay.

    /// <summary>Ô "Cá nhân đăng ký": chủ hồ sơ.</summary>
    [TemplateField("REGISTRANT_NAME")] public string? RegistrantName { get; init; }

    /// <summary>Ô "Người trực tiếp sử dụng": Trưởng đơn vị đã gửi danh mục.</summary>
    [TemplateField("SUPERVISOR_NAME")] public string? SupervisorName { get; init; }

    /// <summary>Ô "Cơ quan tổ chức cán bộ": người rà soát, trình duyệt.</summary>
    [TemplateField("HR_REVIEWER_NAME")] public string? HrReviewerName { get; init; }

    /// <summary>Ô "Cấp có thẩm quyền phê duyệt": người duyệt danh mục.</summary>
    [TemplateField("APPROVER_NAME")] public string? ApproverName { get; init; }

    [TemplateCollection("TASKS")] public List<Mau01TaskRow> Tasks { get; init; } = new();

    /// <summary>Dòng "CỘNG": tổng trọng số các sản phẩm; chưa có sản phẩm → giữ chữ mặc định.</summary>
    [TemplateField("TOTAL_WEIGHT")] public string? TotalWeight { get; init; }

    /// <summary><paramref name="adminParentLine"/>: dòng 1 tiêu đề trái (<see cref="OrgHeaderLine.DirectParent"/>).</summary>
    public static Mau01Data From(EvaluationRecord record, string? adminParentLine = null)
    {
        var tasks = record.Tasks.OrderBy(t => t.TaskOrder).ToList();
        return new Mau01Data
        {
            DateLine = CollectiveFormText.DateLine(record.TasksApprovedAt),
            AdminParentUpper = FormText.OrNull(adminParentLine),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            HasDepartment = Mau09Shared.HasDepartment(record),
            TotalWeight = tasks.Count > 0 ? FormText.Number(tasks.Sum(t => t.Weight), 1) : null,
            Quarter = record.Period != null ? CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            RegistrantName = FormText.OrNull(record.Member?.FullName),
            SupervisorName = record.TasksForwardedById != null ? FormText.OrNull(record.TasksForwardedByName) : null,
            HrReviewerName = record.TasksReviewedById != null ? FormText.OrNull(record.TasksReviewedByName) : null,
            ApproverName = record.TasksApprovedById != null ? FormText.OrNull(record.TasksApprovedByName) : null,
            Tasks = tasks.Select((t, index) => new Mau01TaskRow
            {
                Order = (index + 1).ToString(),
                Name = t.TaskName,
                Code = FormText.OrNull(t.ProductCode),
                Axis = FormText.OrNull(t.AxisCode),
                Role = t.Role switch { TaskRole.ChuTri => "Chủ trì", TaskRole.PhoiHop => "Phối hợp", _ => null },
                Weight = FormText.Number(t.Weight, 1),
                Deadline = FormText.Date(t.Deadline),
                Standard = FormText.OrNull(t.TargetOutput),
                ExceedCriteria = FormText.OrNull(t.ExceedCriteria),
                Evidence = FormText.OrNull(t.EvidenceSource),
                Signer = FormText.OrNull(t.Confirmer)
            }).ToList()
        };
    }
}

public sealed class Mau01TaskRow
{
    [TemplateField("T_STT")] public string? Order { get; init; }
    [TemplateField("T_NAME")] public string? Name { get; init; }

    [TemplateField("T_CODE")] public string? Code { get; init; }

    /// <summary>Theo bộ tiêu chí của kỳ.</summary>
    [TemplateField("T_AXIS")] public string? Axis { get; init; }

    /// <summary>Dữ liệu đăng ký cũ chưa có vai trò → giữ chữ mặc định.</summary>
    [TemplateField("T_ROLE")] public string? Role { get; init; }

    [TemplateField("T_WEIGHT")] public string? Weight { get; init; }
    [TemplateField("T_DEADLINE")] public string? Deadline { get; init; }

    [TemplateField("T_STANDARD")] public string? Standard { get; init; }

    /// <summary>Trống (dữ liệu đăng ký cũ) thì giữ chữ mặc định.</summary>
    [TemplateField("T_EXCEED")] public string? ExceedCriteria { get; init; }

    [TemplateField("T_EVIDENCE")] public string? Evidence { get; init; }

    [TemplateField("T_SIGNER")] public string? Signer { get; init; }
}
