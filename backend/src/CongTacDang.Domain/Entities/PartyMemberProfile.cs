using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public class PartyMemberProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    // Vai 1: Đảng
    public bool IsPartyMember { get; set; } = true;
    public string? PartyCardNumber { get; set; } // Số thẻ Đảng viên
    public DateTime? JoinPartyDate { get; set; } // Ngày vào Đảng
    public DateTime? OfficialPartyDate { get; set; } // Ngày chính thức
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }
    public PartyRole PartyRole { get; set; } = PartyRole.DangVien;

    // Vai 2: Chính quyền
    public Guid? DepartmentId { get; set; }
    public AdministrativeDepartment? Department { get; set; }
    public AdministrativePosition AdminPosition { get; set; } = AdministrativePosition.ChuyenVien;
    public string PositionTitle { get; set; } = string.Empty; // Chức danh hiển thị
    public JobGroup JobGroup { get; set; } = JobGroup.Khung2_AnToanKyThuat;

    // Cấp thẩm quyền duyệt (True: Đảng ủy ATTECH duyệt; False: Trình BTV Đảng ủy TCT duyệt)
    public bool IsApprovedByAttech { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
