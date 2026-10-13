using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>
/// "Ai làm việc gì ở đơn vị nào": các việc trong quy trình đánh giá và người đang làm được từng việc tại một đơn vị
/// (API <c>GET /api/admin/performers</c>).
/// </summary>
public class UnitPerformersDto
{
    /// <summary>Global | Department | PartyCell.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>Id đơn vị chính quyền / tổ chức Đảng đang xem; null khi Toàn công ty.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Tên đơn vị đang xem ("Toàn công ty" khi Global).</summary>
    public string ScopeName { get; set; } = string.Empty;

    /// <summary>Đơn vị đang xem còn hoạt động (đơn vị ngừng hoạt động không nhận bản gán mới).</summary>
    public bool ScopeActive { get; set; } = true;

    /// <summary>
    /// Người đang giữ chức vụ tại đúng đơn vị đang xem (tài khoản đang hoạt động), theo thứ tự chức vụ rồi họ tên;
    /// rỗng khi Toàn công ty. Chỉ để tham khảo khi giao việc — quyền không tự đi theo chức vụ.
    /// </summary>
    public List<PositionHolderDto> Holders { get; set; } = new();

    /// <summary>Các việc theo thứ tự quy trình.</summary>
    public List<PerformerJobDto> Jobs { get; set; } = new();
}

/// <summary>Một người giữ chức vụ tại đơn vị.</summary>
public class PositionHolderDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
}

/// <summary>
/// Bản gán vai trò (có quyền thực hiện bước) cần người quản trị xem lại (API <c>GET /api/admin/performers/review</c>).
/// </summary>
public class PerformerReviewItemDto
{
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Global | Department | PartyCell.</summary>
    public string ScopeType { get; set; } = string.Empty;

    public Guid? ScopeId { get; set; }

    /// <summary>Tên nơi giao.</summary>
    public string ScopeText { get; set; } = string.Empty;

    /// <summary>Hiệu lực đến (không bao gồm); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>NoPositionAtScope (không còn gắn với nơi giao) | ExpiringSoon (giao tạm sắp hết hạn).</summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Lý do cần xem lại (hiển thị).</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Một việc (ứng với một mã quyền thực hiện bước) và những người làm được việc đó tại đơn vị.</summary>
public class PerformerJobDto
{
    /// <summary>Mã quyền thực hiện.</summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của quyền (tên việc).</summary>
    public string PermissionName { get; set; } = string.Empty;

    /// <summary>Người làm được việc này tại đơn vị (mỗi dòng ứng với một bản gán cấp quyền).</summary>
    public List<PerformerDto> People { get; set; } = new();

    /// <summary>Vai trò có chứa quyền này và gán được tại đơn vị đang xem (dùng cho thao tác "Thêm người").</summary>
    public List<PerformerRoleOptionDto> Roles { get; set; } = new();
}

/// <summary>Một người làm được việc tại đơn vị, qua một bản gán vai trò.</summary>
public class PerformerDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>Vai trò cấp quyền.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Loại phạm vi của bản gán: Global | Department | PartyCell.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>Đơn vị được gán (null khi Toàn công ty).</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Tên nơi gán ("Toàn công ty" hoặc tên đơn vị).</summary>
    public string ScopeText { get; set; } = string.Empty;

    /// <summary>Bản gán nguồn.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>Bản gán đặt tại đúng đơn vị đang xem (gỡ được ở màn hình này); false = gán ở đơn vị cấp trên / Toàn công ty.</summary>
    public bool AssignedHere { get; set; }

    /// <summary>Hiệu lực đến (không bao gồm); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>
    /// Quan hệ của người được giao với nơi giao: tên các chức vụ đang giữ tại nơi giao hoặc đơn vị cấp trên của nó
    /// (nối bằng ", "); "Cán bộ của đơn vị" nếu không giữ chức vụ nhưng thuộc nơi giao (hoặc đơn vị con);
    /// null nếu không có quan hệ nào hoặc bản gán Toàn công ty.
    /// </summary>
    public string? PositionAtScope { get; set; }
}

/// <summary>Vai trò chọn được khi thêm người cho một việc.</summary>
public class PerformerRoleOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
