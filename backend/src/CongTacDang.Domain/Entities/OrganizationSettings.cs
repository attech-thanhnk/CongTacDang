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

    public const int MaxFileNameLength = 255;

    public const int MaxGuideNumberLength = 50;

    public const string PanelCenter = "center";
    public const string PanelLeft = "left";
    public const string PanelRight = "right";

    /// <summary>Giá trị hợp lệ của <see cref="LoginPanelPosition"/>.</summary>
    public static readonly string[] LoginPanelPositions = [PanelCenter, PanelLeft, PanelRight];

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

    /// <summary>Số văn bản hướng dẫn in ở dòng "Căn cứ Hướng dẫn số …" (ví dụ "03-HD/TVĐU"); null = để dòng chấm.</summary>
    public string? GuideDocumentNumber { get; set; }

    /// <summary>Ngày văn bản hướng dẫn; null = để dòng chấm.</summary>
    public DateOnly? GuideDocumentDate { get; set; }

    /// <summary>Tên hệ thống trên giao diện.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Khóa tệp ảnh nền màn hình đăng nhập trong kho tệp; null = không dùng ảnh.</summary>
    public string? LoginBackgroundKey { get; set; }

    public string? LoginBackgroundContentType { get; set; }

    public string? LoginBackgroundFileName { get; set; }

    /// <summary>UTC; làm phiên bản trong URL ảnh để trình duyệt không giữ ảnh cũ.</summary>
    public DateTime? LoginBackgroundUpdatedAt { get; set; }

    /// <summary><c>center</c> | <c>left</c> | <c>right</c>.</summary>
    public string LoginPanelPosition { get; set; } = PanelCenter;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
