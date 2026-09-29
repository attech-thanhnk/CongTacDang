using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thông tin đơn vị dùng trên biểu mẫu, báo cáo và giao diện (bảng <c>organization_settings</c>, task 17 — T-80).
/// Một bản ghi duy nhất (<see cref="SingletonId"/>): các trường cố định, có kiểu, dễ kiểm tra hợp lệ — không dùng bảng
/// khóa–giá trị vì danh sách thông tin do code quyết định (mỗi trường gắn với một Tag biểu mẫu / vị trí trên báo cáo).
/// Không có giá trị nào ghi cứng trong code xuất biểu mẫu; giá trị mặc định chỉ nằm ở DataSeeder.
/// </summary>
public class OrganizationSettings : IAuditableEntity
{
    /// <summary>Id cố định của bản ghi duy nhất.</summary>
    public static readonly Guid SingletonId = new("7f1d7c55-0a5e-4d43-9d4b-3f0c2b6a1e17");

    /// <summary>Độ dài tối đa của tên (Đảng bộ, công ty…).</summary>
    public const int MaxNameLength = 300;

    /// <summary>Độ dài tối đa của tên viết tắt.</summary>
    public const int MaxShortNameLength = 30;

    /// <summary>Độ dài tối đa của địa danh.</summary>
    public const int MaxLocationLength = 100;

    /// <summary>Mã định danh (luôn bằng <see cref="SingletonId"/>).</summary>
    public Guid Id { get; set; } = SingletonId;

    /// <summary>Tên Đảng bộ — dòng tiêu đề trái của biểu mẫu, báo cáo (ví dụ "ĐẢNG BỘ CÔNG TY …"). Ghi đúng như in.</summary>
    public string PartyCommitteeName { get; set; } = string.Empty;

    /// <summary>Tên tổ chức Đảng cấp trên — dòng trên cùng của tiêu đề trái (ví dụ "ĐẢNG BỘ TỔNG CÔNG TY …"). Ghi đúng như in.</summary>
    public string SuperiorPartyName { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ của công ty (chữ thường, biểu mẫu tự in hoa khi cần).</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Tên đơn vị chủ quản cấp trên của công ty (Tổng công ty…).</summary>
    public string ParentCompanyName { get; set; } = string.Empty;

    /// <summary>Tên viết tắt — dùng trong tên tệp tải xuống và tiêu đề ngắn (chỉ chữ, số, <c>-</c>, <c>_</c>).</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Địa danh ghi ở dòng "…, ngày … tháng … năm …".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của hệ thống trên giao diện (thanh bên, tiêu đề, trang đăng nhập).</summary>
    public string SystemName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
