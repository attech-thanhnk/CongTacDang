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

    /// <summary>Các việc theo thứ tự quy trình.</summary>
    public List<PerformerJobDto> Jobs { get; set; } = new();
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
}

/// <summary>Vai trò chọn được khi thêm người cho một việc.</summary>
public class PerformerRoleOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
