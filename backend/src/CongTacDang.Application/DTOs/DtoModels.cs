using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Thông tin hồ sơ và vai trò của cán bộ đang đăng nhập</summary>
public class UserProfileDto
{
    /// <summary>Mã định danh duy nhất của cán bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Tên tài khoản đăng nhập</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Chức danh hiển thị trên văn bản (mặc định = chức vụ chính)</summary>
    public string AdminTitle { get; set; } = string.Empty;

    /// <summary>Tên Chi bộ Đảng sinh hoạt</summary>
    public string PartyBranchName { get; set; } = string.Empty;

    /// <summary>Tên đơn vị / Phòng ban chuyên môn</summary>
    public string AdminDeptName { get; set; } = string.Empty;

    /// <summary>Mã khung tỷ trọng A-B-C-D mặc định (theo bộ tiêu chí); null nếu chưa chọn.</summary>
    public string? WeightFrameCode { get; set; }

    /// <summary>Danh sách mã vai trò hệ thống</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>Danh sách mã quyền hạn nguyên tử được cấp</summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>Người dùng phải đổi mật khẩu tạm trước khi tiếp tục</summary>
    public bool MustChangePassword { get; set; }
}

/// <summary>Một nút cây đơn vị (tổ chức Đảng hoặc đơn vị chính quyền) trong danh sách phẳng sắp theo cây.</summary>
public abstract class OrgUnitDto
{
    /// <summary>Mã định danh đơn vị</summary>
    public Guid Id { get; set; }

    /// <summary>Mã ký hiệu</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên đơn vị</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị trong cùng cấp</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang hoạt động (false = đã ngừng hoạt động)</summary>
    public bool IsActive { get; set; }

    /// <summary>Số cán bộ có đơn vị này là nơi sinh hoạt Đảng / đơn vị công tác chính</summary>
    public int MemberCount { get; set; }

    /// <summary>Đơn vị cha (null = gốc)</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Tên đơn vị cha</summary>
    public string? ParentName { get; set; }

    /// <summary>Loại đơn vị</summary>
    public Guid? UnitTypeId { get; set; }

    /// <summary>Tên loại đơn vị</summary>
    public string? UnitTypeName { get; set; }

    /// <summary>Độ sâu trong cây (gốc = 0)</summary>
    public int Depth { get; set; }

    /// <summary>Đường dẫn vật hóa <c>/id gốc/…/id/</c></summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Số đơn vị con trực tiếp (chưa xóa)</summary>
    public int ChildCount { get; set; }
}

/// <summary>Tổ chức Đảng (Đảng ủy, Đảng bộ bộ phận, Chi bộ…) — API giữ tên "branches".</summary>
public class BranchDto : OrgUnitDto
{
}

/// <summary>Đơn vị chính quyền (Công ty, Phòng, Trung tâm…) — API giữ tên "departments".</summary>
public class DepartmentDto : OrgUnitDto
{
}

/// <summary>Thông tin tệp tin đính kèm và văn bản minh chứng</summary>
public class AttachmentDto
{
    /// <summary>Mã định danh tệp tin</summary>
    public Guid Id { get; set; }

    /// <summary>Tên tệp tin gốc</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Định dạng MIME của tệp</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Dung lượng tệp tính bằng byte</summary>
    public long FileSize { get; set; }

    /// <summary>Mã biểu mẫu hoặc ký hiệu hồ sơ (M01, M02, M10...)</summary>
    public string FormCode { get; set; } = string.Empty;

    /// <summary>Trích yếu hoặc nội dung tóm tắt văn bản</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Mã băm SHA-256 bảo đảm toàn vẹn dữ liệu</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Đường dẫn tải về trực tiếp (nếu có)</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Thời điểm tải lên hệ thống</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>Họ tên hoặc tài khoản người tải lên</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>Mã người tải lên phiên bản này (null với dữ liệu cũ)</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>Loại đối tượng sở hữu tệp (General, EvaluationRecord, EvaluationTask)</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>Mã đối tượng sở hữu tệp</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Mã nhóm phiên bản (dùng để xem lịch sử, tải phiên bản cụ thể)</summary>
    public Guid FileGroupId { get; set; }

    /// <summary>Số thứ tự phiên bản</summary>
    public int VersionNumber { get; set; }

    /// <summary>Là phiên bản hiện hành</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Thời điểm phiên bản bị thay (null nếu đang hiện hành)</summary>
    public DateTime? SupersededAt { get; set; }

    /// <summary>Người đã thay phiên bản này</summary>
    public Guid? SupersededById { get; set; }
}

/// <summary>Thông tin một bản ghi audit dành cho khu vực quản trị.</summary>
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


/// <summary>Payload đăng nhập từ client</summary>
public class LoginRequestDto
{
    /// <summary>Tên tài khoản đăng nhập</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu xác thực</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Kết quả đăng nhập — trả về thông tin cơ bản, JWT set qua HttpOnly Cookie</summary>
public class LoginResponseDto
{
    /// <summary>Mã định danh cán bộ</summary>
    public Guid Id { get; set; }

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Tên tài khoản</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Tên các vai trò từ bản gán đang hiệu lực (chỉ để hiển thị, không dùng để phân quyền)</summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>Các mã quyền có ở ít nhất một phạm vi</summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>Quyền kèm phạm vi: <c>{ code, scopeType, scopeId, scopeName }</c></summary>
    public List<AccessGrantDto> Grants { get; set; } = new();

    /// <summary>Người dùng phải đổi mật khẩu tạm trước khi tiếp tục</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Thời điểm hết hạn của phiên làm việc</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Kết quả xác thực nội bộ chuyển từ Application Service về Controller</summary>
public class AuthResultDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public LoginResponseDto UserResponse { get; set; } = null!;
}

/// <summary>Payload đổi mật khẩu của người dùng đang đăng nhập.</summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Kết quả đặt lại mật khẩu tạm cho cán bộ.</summary>
public class ResetPasswordResponseDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}

/// <summary>Yêu cầu cập nhật quyền hạn cho vai trò</summary>
public class UpdateRolePermissionsDto
{
    /// <summary>Danh sách mã quyền hạn nguyên tử cần cấp cho vai trò</summary>
    public List<string> PermissionCodes { get; set; } = new();
}

