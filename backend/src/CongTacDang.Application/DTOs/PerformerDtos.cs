using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Các việc trong quy trình đánh giá và người làm được từng việc tại một đơn vị.</summary>
public class UnitPerformersDto
{
    /// <summary>Global | Department | PartyCell.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>null khi Toàn công ty.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>"Toàn công ty" khi Global.</summary>
    public string ScopeName { get; set; } = string.Empty;

    /// <summary>Đơn vị ngừng hoạt động không nhận bản gán mới.</summary>
    public bool ScopeActive { get; set; } = true;

    /// <summary>Người giữ chức vụ tại đúng đơn vị đang xem; rỗng khi Toàn công ty.
    /// Chỉ để tham khảo khi giao việc — quyền không tự đi theo chức vụ.</summary>
    public List<PositionHolderDto> Holders { get; set; } = new();

    /// <summary>Theo thứ tự quy trình.</summary>
    public List<PerformerJobDto> Jobs { get; set; } = new();
}

public class PositionHolderDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
}

/// <summary>Bản gán vai trò (có quyền thực hiện bước) cần quản trị xem lại.</summary>
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

    /// <summary>Không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>NoPositionAtScope (không còn gắn với nơi giao) | ExpiringSoon (giao tạm sắp hết hạn).</summary>
    public string ReasonCode { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Một việc (ứng với một mã quyền thực hiện bước) và những người làm được việc đó tại đơn vị.</summary>
public class PerformerJobDto
{
    public string Permission { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;

    /// <summary>Có kỳ dự thảo/mở/khóa dùng việc này; không có kỳ nào như vậy → true.</summary>
    public bool InUse { get; set; } = true;

    /// <summary>Mỗi dòng ứng với một bản gán cấp quyền.</summary>
    public List<PerformerDto> People { get; set; } = new();

    /// <summary>Vai trò chứa quyền này và gán được tại đơn vị đang xem (cho "Thêm người").</summary>
    public List<PerformerRoleOptionDto> Roles { get; set; } = new();
}

/// <summary>Một người làm được việc tại đơn vị, qua một bản gán vai trò.</summary>
public class PerformerDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>Global | Department | PartyCell.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>null khi Toàn công ty.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>"Toàn công ty" hoặc tên đơn vị.</summary>
    public string ScopeText { get; set; } = string.Empty;

    public Guid AssignmentId { get; set; }

    /// <summary>Gán tại đúng đơn vị đang xem (gỡ được ở đây); false = gán ở cấp trên / Toàn công ty.</summary>
    public bool AssignedHere { get; set; }

    /// <summary>Không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Chức vụ giữ tại nơi giao hoặc cấp trên (nối ", "); "Cán bộ của đơn vị" nếu chỉ thuộc nơi giao;
    /// null nếu không có quan hệ hoặc bản gán Toàn công ty.</summary>
    public string? PositionAtScope { get; set; }
}

/// <summary>Vai trò chọn được khi thêm người cho một việc.</summary>
public class PerformerRoleOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
