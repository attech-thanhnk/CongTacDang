using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    /// <summary>Chức danh trên văn bản; mặc định = chức vụ chính.</summary>
    public string AdminTitle { get; set; } = string.Empty;

    public string PartyBranchName { get; set; } = string.Empty;

    public string AdminDeptName { get; set; } = string.Empty;

    /// <summary>Theo bộ tiêu chí; null nếu chưa chọn.</summary>
    public string? WeightFrameCode { get; set; }

    public string[] Roles { get; set; } = Array.Empty<string>();

    public string[] Permissions { get; set; } = Array.Empty<string>();

    public bool MustChangePassword { get; set; }
}

/// <summary>Nút cây đơn vị trong danh sách phẳng sắp theo cây.</summary>
public abstract class OrgUnitDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Trong cùng cấp.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Số cán bộ sinh hoạt Đảng / công tác chính tại đơn vị.</summary>
    public int MemberCount { get; set; }

    /// <summary>null = gốc.</summary>
    public Guid? ParentId { get; set; }

    public string? ParentName { get; set; }

    public Guid? UnitTypeId { get; set; }

    public string? UnitTypeName { get; set; }

    /// <summary>Gốc = 0.</summary>
    public int Depth { get; set; }

    /// <summary>Đường dẫn vật hóa <c>/id gốc/…/id/</c>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Con trực tiếp, chưa xóa.</summary>
    public int ChildCount { get; set; }
}

/// <summary>Tổ chức Đảng; API giữ tên "branches".</summary>
public class BranchDto : OrgUnitDto
{
}

/// <summary>Đơn vị chính quyền; API giữ tên "departments".</summary>
public class DepartmentDto : OrgUnitDto
{
}

/// <summary>Thao tác được làm với minh chứng cá nhân của hồ sơ, theo quyền và giai đoạn; giao diện hiện nút theo các cờ này.</summary>
public class EvidenceRightsDto
{
    public Guid RecordId { get; set; }

    /// <summary>Open (thêm/thay/gỡ) | AddOnly (chỉ bổ sung) | Locked.</summary>
    public string Stage { get; set; } = string.Empty;

    public bool CanAdd { get; set; }

    public bool CanReplace { get; set; }

    public bool CanRemove { get; set; }

    /// <summary>Chỉ với chủ hồ sơ; null khi được làm mọi thao tác.</summary>
    public string? Reason { get; set; }
}

public class AttachmentDto
{
    public Guid Id { get; set; }

    /// <summary>Tên gốc.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>Byte.</summary>
    public long FileSize { get; set; }

    /// <summary>M01, M02, M10…</summary>
    public string FormCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>SHA-256.</summary>
    public string Checksum { get; set; } = string.Empty;

    public string? DownloadUrl { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>Họ tên hoặc tài khoản.</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>null với dữ liệu cũ.</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>EvaluationRecord / EvaluationTask / Unlinked (chưa gắn, chỉ người tải lên thấy).</summary>
    public string OwnerType { get; set; } = string.Empty;

    public Guid? OwnerId { get; set; }

    /// <summary>Nhóm các phiên bản của cùng một tệp.</summary>
    public Guid FileGroupId { get; set; }

    public int VersionNumber { get; set; }

    public bool IsCurrent { get; set; }

    /// <summary>null nếu đang hiện hành.</summary>
    public DateTime? SupersededAt { get; set; }

    public Guid? SupersededById { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OldValues { get; set; } = "{}";
    public string NewValues { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Số đếm tính trên toàn bộ bản ghi khớp bộ lọc.</summary>
public class AuditLogPageDto
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Theo loại thao tác (Create, Update, Delete, Import…).</summary>
    public Dictionary<string, int> ActionCounts { get; set; } = new();

    public int ActorCount { get; set; }

    /// <summary>Để chọn bộ lọc.</summary>
    public List<string> EntityTypes { get; set; } = new();
}

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

/// <summary>JWT đặt qua HttpOnly cookie, không trả trong body.</summary>
public class LoginResponseDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    /// <summary>Chỉ để hiển thị, không dùng để phân quyền.</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>Mã quyền có ở ít nhất một phạm vi.</summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    public List<AccessGrantDto> Grants { get; set; } = new();

    public bool MustChangePassword { get; set; }

    public DateTime ExpiresAt { get; set; }
}

/// <summary>Nội bộ: Application Service → Controller.</summary>
public class AuthResultDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public LoginResponseDto UserResponse { get; set; } = null!;
}

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordResponseDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}

public class UpdateRolePermissionsDto
{
    public List<string> PermissionCodes { get; set; } = new();
}

