using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể hồ sơ Cán bộ lãnh đạo, quản lý 2 vai (Đảng vụ và Chính quyền) theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class PartyMemberProfile : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh cán bộ</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên tài khoản đăng nhập</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu băm bảo mật</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Họ và tên cán bộ</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Thư điện tử liên hệ</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên hệ</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Đường dẫn ảnh đại diện</summary>
    public string? AvatarUrl { get; set; }

    #region Vai 1: Công tác Đảng

    /// <summary>Đã kết nạp Đảng hay chưa</summary>
    public bool IsPartyMember { get; set; } = true;

    /// <summary>Số thẻ Đảng viên</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Ngày vào Đảng (dự bị)</summary>
    public DateTime? JoinPartyDate { get; set; }

    /// <summary>Ngày công nhận chính thức</summary>
    public DateTime? OfficialPartyDate { get; set; }

    /// <summary>Mã Chi bộ Đảng đang sinh hoạt</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đối tượng Chi bộ Đảng sinh hoạt</summary>
    public PartyCell? PartyCell { get; set; }

    /// <summary>Chức vụ công tác Đảng (Bí thư, Phó Bí thư, Chi ủy viên, Đảng viên)</summary>
    public PartyRole PartyRole { get; set; } = PartyRole.DangVien;

    #endregion

    #region Vai 2: Chính quyền & Chuyên môn

    /// <summary>Mã Phòng ban / Đơn vị chuyên môn</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Đối tượng Phòng ban chuyên môn</summary>
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Chức vụ chính quyền</summary>
    public AdministrativePosition AdminPosition { get; set; } = AdministrativePosition.ChuyenVien;

    /// <summary>Chức danh quản lý hiển thị trên văn bản</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Khối chức danh công tác theo 03-HD/TVĐU</summary>
    public JobGroup JobGroup { get; set; } = JobGroup.Khung2_AnToanKyThuat;

    #endregion

    /// <summary>Cấp thẩm quyền duyệt (True: Đảng ủy ATTECH duyệt; False: Trình BTV Đảng ủy Tổng công ty duyệt)</summary>
    public bool IsApprovedByAttech { get; set; } = true;

    /// <summary>Trạng thái tài khoản (kích hoạt/vô hiệu hóa)</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Buộc người dùng đổi mật khẩu tạm ở lần đăng nhập kế tiếp</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Số lần đăng nhập sai liên tiếp</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Thời điểm kết thúc khóa đăng nhập tạm thời</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>Thời điểm khởi tạo hồ sơ</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách vai trò quyền hạn được gán cho cán bộ (Dynamic RBAC)</summary>
    public ICollection<AppRole> Roles { get; set; } = new List<AppRole>();
}
