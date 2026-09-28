using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Chi bộ trực thuộc Đảng bộ Công ty ATTECH
/// </summary>
public class PartyCell : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh Chi bộ</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã ký hiệu Chi bộ (ví dụ: CB-KT, CB-VP)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ của Chi bộ Đảng</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả nhiệm vụ chính trị trọng tâm</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Mã cán bộ giữ chức vụ Bí thư Chi bộ</summary>
    public Guid? SecretaryId { get; set; }

    /// <summary>Mã cán bộ giữ chức vụ Phó Bí thư Chi bộ</summary>
    public Guid? DeputySecretaryId { get; set; }

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm tạo Chi bộ trên hệ thống</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách cán bộ, Đảng viên sinh hoạt tại Chi bộ</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}

/// <summary>
/// Thực thể Phòng ban / Phân xưởng chuyên môn thuộc Công ty ATTECH
/// </summary>
public class AdministrativeDepartment : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh đơn vị chuyên môn</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã ký hiệu phòng/xưởng (ví dụ: P-KT, X-DV)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên phòng ban / phân xưởng chuyên môn</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả chức năng nhiệm vụ chuyên môn</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Mã cán bộ giữ chức danh Trưởng phòng / Quản đốc</summary>
    public Guid? HeadId { get; set; }

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách cán bộ công tác tại đơn vị chuyên môn</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}
