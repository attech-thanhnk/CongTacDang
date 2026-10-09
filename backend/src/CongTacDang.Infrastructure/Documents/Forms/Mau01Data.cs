using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 01 — Phiếu giao/đăng ký sản phẩm, công việc chuyên môn hằng quý.
/// Template: <c>Mau_01_PhieuGiaoNhiemVu.docx</c>. Trường để null giữ chữ mặc định trong template.
/// </summary>
public sealed class Mau01Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_01_PhieuGiaoNhiemVu.docx";

    /// <summary>"ngày … tháng … năm …" sau địa danh: ngày duyệt danh mục nhiệm vụ (chưa duyệt → giữ dòng chấm).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("DEPARTMENT")] public string? Department { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }

    /// <summary>Chưa có dữ liệu người quản lý trực tiếp — giữ chữ mặc định trong template.</summary>
    [TemplateField("SUPERVISOR_NAME")] public string? SupervisorName { get; init; }

    [TemplateCollection("TASKS")] public List<Mau01TaskRow> Tasks { get; init; } = new();

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu (nạp kèm Period, Member, Department, PartyCell, Tasks).</summary>
    public static Mau01Data From(EvaluationRecord record)
    {
        var tasks = record.Tasks.OrderBy(t => t.TaskOrder).ToList();
        return new Mau01Data
        {
            DateLine = CollectiveFormText.DateLine(record.TasksApprovedAt),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            Quarter = record.Period != null ? FormText.Quarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
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

/// <summary>Một dòng nhiệm vụ của Mẫu 01.</summary>
public sealed class Mau01TaskRow
{
    [TemplateField("T_STT")] public string? Order { get; init; }
    [TemplateField("T_NAME")] public string? Name { get; init; }

    /// <summary>Mã sản phẩm (không bắt buộc) — trống thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_CODE")] public string? Code { get; init; }

    /// <summary>Mã trục kết quả theo bộ tiêu chí của kỳ; nhiệm vụ chưa chọn trục → giữ chữ mặc định trong template.</summary>
    [TemplateField("T_AXIS")] public string? Axis { get; init; }

    /// <summary>Vai trò "Chủ trì" / "Phối hợp"; dữ liệu đăng ký cũ chưa có → giữ chữ mặc định trong template.</summary>
    [TemplateField("T_ROLE")] public string? Role { get; init; }

    [TemplateField("T_WEIGHT")] public string? Weight { get; init; }
    [TemplateField("T_DEADLINE")] public string? Deadline { get; init; }

    /// <summary>Chuẩn đạt đã đăng ký; trống thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_STANDARD")] public string? Standard { get; init; }

    /// <summary>Trống (dữ liệu đăng ký cũ) thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_EXCEED")] public string? ExceedCriteria { get; init; }

    /// <summary>Trống (dữ liệu đăng ký cũ) thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_EVIDENCE")] public string? Evidence { get; init; }

    /// <summary>Trống (dữ liệu đăng ký cũ) thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_SIGNER")] public string? Signer { get; init; }
}
