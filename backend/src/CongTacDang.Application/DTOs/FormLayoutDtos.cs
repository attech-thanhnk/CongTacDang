using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một kiểu trình bày biểu mẫu tự đánh giá (Mẫu 09C, 9D).</summary>
public class FormLayoutDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Mẫu 9D.</summary>
    public bool SingleLinePositions { get; set; }

    public bool ShowDateLineOn9D { get; set; }

    /// <summary>Chữ in ở dòng chức vụ không có; null = dòng chấm.</summary>
    public string? EmptyPositionText { get; set; }

    /// <summary>Áp dụng cho cán bộ không có chức vụ nào gắn kiểu.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Nhỏ được ưu tiên khi cán bộ giữ nhiều chức vụ gắn kiểu khác nhau.</summary>
    public int SortOrder { get; set; }

    public int PositionCount { get; set; }

    /// <summary>Số cán bộ đặt riêng kiểu này.</summary>
    public int MemberCount { get; set; }
}

/// <summary>Thêm/sửa kiểu trình bày (gửi đủ các trường).</summary>
public class SaveFormLayoutDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool SingleLinePositions { get; set; }
    public bool ShowDateLineOn9D { get; set; }
    public string? EmptyPositionText { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Kiểu trình bày của cán bộ (<c>GET /api/users/{id}/form-layout</c>).</summary>
public class MemberFormLayoutDto
{
    /// <summary>null = chưa có kiểu nào (in như template).</summary>
    public Guid? EffectiveId { get; set; }

    public string? EffectiveName { get; set; }

    /// <summary>Suy ra từ chức vụ (hoặc kiểu mặc định), không tính đặt riêng.</summary>
    public Guid? DerivedId { get; set; }

    public string? DerivedName { get; set; }

    /// <summary>null = theo chức vụ.</summary>
    public Guid? OverrideId { get; set; }

    /// <summary>Tên chức vụ → tên kiểu, làm căn cứ suy ra.</summary>
    public List<string> PositionLayouts { get; set; } = new();
}

/// <summary>Đặt riêng / bỏ đặt riêng kiểu trình bày cho cán bộ (<c>PUT /api/users/{id}/form-layout</c>).</summary>
public class SetMemberFormLayoutDto
{
    /// <summary>null → theo chức vụ.</summary>
    public Guid? LayoutId { get; set; }
}
