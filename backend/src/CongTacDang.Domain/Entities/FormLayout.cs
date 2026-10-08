using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Domain.Entities;

/// <summary>Tiêu đề trái của Mẫu 09B.</summary>
public enum FormHeaderStyle
{
    /// <summary>"ĐẢNG BỘ …" (Đảng bộ cơ sở) / "CHI BỘ …" (Chi bộ sinh hoạt của hồ sơ).</summary>
    PartyCell = 0,

    /// <summary>"ĐẢNG BỘ …" (tổ chức Đảng cấp trên) / "ĐẢNG ỦY …" (cấp ủy cơ sở) — như phiếu của Bí thư Đảng ủy.</summary>
    PartyCommittee = 1
}

/// <summary>
/// Kiểu trình bày biểu mẫu tự đánh giá (bảng <c>form_layouts</c>): cách in phần đầu, chức vụ và chữ ký của Mẫu 09B, 09C, 9D.
/// Quản trị tạo/sửa trong danh mục; gán mặc định theo chức vụ (<see cref="Position.FormLayoutId"/>), ghi đè được cho từng cán bộ
/// (<see cref="PartyMemberProfile.FormLayoutOverrideId"/>).
/// </summary>
public class FormLayout : IAuditableEntity, ISoftDeletable
{
    /// <summary>Độ dài tối đa của tên kiểu.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Độ dài tối đa của mô tả.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>Độ dài tối đa của chữ in thay chức vụ trống.</summary>
    public const int MaxEmptyPositionTextLength = 50;

    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên kiểu (duy nhất trong các kiểu chưa xóa).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả (áp dụng cho ai).</summary>
    public string? Description { get; set; }

    /// <summary>Tiêu đề trái của Mẫu 09B.</summary>
    public FormHeaderStyle HeaderStyle { get; set; } = FormHeaderStyle.PartyCell;

    /// <summary>Mẫu 09B có ô "Xác nhận của chi bộ".</summary>
    public bool ShowCellConfirmation { get; set; } = true;

    /// <summary>Mẫu 09B, 9D in chức vụ trên một dòng "Chức vụ (Đảng, Đoàn thể, Chính quyền): …" thay cho 3 dòng.</summary>
    public bool SingleLinePositions { get; set; }

    /// <summary>Mẫu 9D có dòng "Địa danh, ngày … tháng … năm …" dưới "ĐẢNG CỘNG SẢN VIỆT NAM".</summary>
    public bool ShowDateLineOn9D { get; set; }

    /// <summary>Chữ in ở dòng chức vụ không có (ví dụ "không"); null = giữ dòng chấm để viết tay.</summary>
    public string? EmptyPositionText { get; set; }

    /// <summary>Kiểu mặc định cho cán bộ không có chức vụ nào gắn kiểu (tối đa một kiểu).</summary>
    public bool IsDefault { get; set; }

    /// <summary>Thứ tự hiển thị; cán bộ giữ nhiều chức vụ gắn kiểu khác nhau thì kiểu có thứ tự nhỏ hơn được dùng.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Kiểu áp dụng cho cán bộ: kiểu ghi đè của cán bộ; không có thì kiểu của các chức vụ đang giữ (thứ tự nhỏ nhất, rồi theo tên);
    /// không có thì kiểu mặc định. Null = chưa có kiểu nào (biểu mẫu in như template).
    /// </summary>
    public static FormLayout? Resolve(FormLayout? memberOverride, IEnumerable<FormLayout?> positionLayouts, FormLayout? defaultLayout)
    {
        ArgumentNullException.ThrowIfNull(positionLayouts);
        return memberOverride
            ?? positionLayouts.Where(l => l != null).OrderBy(l => l!.SortOrder).ThenBy(l => l!.Name, StringComparer.Ordinal).FirstOrDefault()
            ?? defaultLayout;
    }
}
