using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Thông tin đơn vị cho biểu mẫu, báo cáo, giao diện; một bản ghi duy nhất (<see cref="SingletonId"/>).
/// Giá trị mặc định chỉ nằm ở DataSeeder, không ghi cứng trong code.</summary>
public class OrganizationSettings : IAuditableEntity
{
    public static readonly Guid SingletonId = new("7f1d7c55-0a5e-4d43-9d4b-3f0c2b6a1e17");

    /// <summary>Tên Đảng bộ, công ty…</summary>
    public const int MaxNameLength = 300;

    public const int MaxShortNameLength = 30;

    public const int MaxLocationLength = 100;

    /// <summary>Luôn bằng <see cref="SingletonId"/>.</summary>
    public Guid Id { get; set; } = SingletonId;

    /// <summary>Dòng tiêu đề trái của biểu mẫu (ví dụ "ĐẢNG BỘ CÔNG TY …"); ghi đúng như in.</summary>
    public string PartyCommitteeName { get; set; } = string.Empty;

    /// <summary>Dòng trên cùng của tiêu đề trái (ví dụ "ĐẢNG BỘ TỔNG CÔNG TY …"); ghi đúng như in.</summary>
    public string SuperiorPartyName { get; set; } = string.Empty;

    /// <summary>Chữ thường; biểu mẫu tự in hoa khi cần.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Đơn vị chủ quản cấp trên (Tổng công ty…).</summary>
    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Dùng trong tên tệp tải xuống; chỉ chữ, số, <c>-</c>, <c>_</c>.</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Ghi ở dòng "…, ngày … tháng … năm …".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Dòng thứ hai tiêu đề trái Mẫu 10; rỗng thì giữ chữ "CƠ QUAN THẨM ĐỊNH" của biểu mẫu.</summary>
    public string AppraisalUnitName { get; set; } = string.Empty;

    /// <summary>Tên hệ thống trên giao diện.</summary>
    public string SystemName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
