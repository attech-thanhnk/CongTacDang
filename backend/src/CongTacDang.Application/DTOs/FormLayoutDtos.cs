using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một kiểu trình bày biểu mẫu tự đánh giá (Mẫu 09B, 09C, 9D).</summary>
public class FormLayoutDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Tiêu đề trái Mẫu 09B: <c>PartyCell</c> (Đảng bộ / Chi bộ) hoặc <c>PartyCommittee</c> (Đảng ủy).</summary>
    public string HeaderStyle { get; set; } = string.Empty;

    /// <summary>Ô "Xác nhận của chi bộ" trên Mẫu 09B.</summary>
    public bool ShowCellConfirmation { get; set; }

    /// <summary>Mẫu 09B, 9D.</summary>
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

    /// <summary><c>PartyCell</c> / <c>PartyCommittee</c>.</summary>
    public string? HeaderStyle { get; set; }

    public bool ShowCellConfirmation { get; set; } = true;
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
