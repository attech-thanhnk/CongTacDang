using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Domain.Entities;

/// <summary>Tiêu đề trái của Mẫu 09B.</summary>
public enum FormHeaderStyle
{
    /// <summary>"ĐẢNG BỘ …" (Đảng bộ cơ sở) / "CHI BỘ …" (Chi bộ sinh hoạt của hồ sơ).</summary>
    PartyCell = 0,

    /// <summary>"ĐẢNG BỘ …" (cấp trên) / "ĐẢNG ỦY …" (cấp ủy cơ sở), như phiếu của Bí thư Đảng ủy.</summary>
    PartyCommittee = 1
}

/// <summary>Cách in phần đầu, chức vụ, chữ ký của Mẫu 09B, 09C, 9D. Gán mặc định theo chức vụ, ghi đè được cho từng cán bộ.</summary>
public class FormLayout : IAuditableEntity, ISoftDeletable
{
    public const int MaxNameLength = 200;

    public const int MaxDescriptionLength = 500;

    /// <summary>Chữ in thay chức vụ trống.</summary>
    public const int MaxEmptyPositionTextLength = 50;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Duy nhất trong các kiểu chưa xóa.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Áp dụng cho ai.</summary>
    public string? Description { get; set; }

    public FormHeaderStyle HeaderStyle { get; set; } = FormHeaderStyle.PartyCell;

    /// <summary>Mẫu 09B có ô "Xác nhận của chi bộ".</summary>
    public bool ShowCellConfirmation { get; set; } = true;

    /// <summary>In chức vụ trên một dòng "Chức vụ (Đảng, Đoàn thể, Chính quyền): …" thay cho 3 dòng (09B, 9D).</summary>
    public bool SingleLinePositions { get; set; }

    /// <summary>Mẫu 9D có dòng "Địa danh, ngày … tháng … năm …" dưới "ĐẢNG CỘNG SẢN VIỆT NAM".</summary>
    public bool ShowDateLineOn9D { get; set; }

    /// <summary>Chữ in ở dòng chức vụ không có (ví dụ "không"); null = giữ dòng chấm để viết tay.</summary>
    public string? EmptyPositionText { get; set; }

    /// <summary>Cho cán bộ không có chức vụ nào gắn kiểu; tối đa một kiểu.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Cán bộ giữ nhiều chức vụ gắn kiểu khác nhau thì dùng kiểu có thứ tự nhỏ hơn.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Ghi đè của cán bộ → kiểu của chức vụ đang giữ (thứ tự nhỏ nhất, rồi theo tên) → kiểu mặc định.
    /// Null = biểu mẫu in như template.</summary>
    public static FormLayout? Resolve(FormLayout? memberOverride, IEnumerable<FormLayout?> positionLayouts, FormLayout? defaultLayout)
    {
        ArgumentNullException.ThrowIfNull(positionLayouts);
        return memberOverride
            ?? positionLayouts.Where(l => l != null).OrderBy(l => l!.SortOrder).ThenBy(l => l!.Name, StringComparer.Ordinal).FirstOrDefault()
            ?? defaultLayout;
    }
}
