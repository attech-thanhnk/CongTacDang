using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Hợp đồng chung của một nút trong cây đơn vị (tổ chức Đảng hoặc đơn vị chính quyền).
/// </summary>
public interface IOrgUnit
{
    /// <summary>Mã định danh đơn vị.</summary>
    Guid Id { get; }

    /// <summary>Mã ký hiệu.</summary>
    string Code { get; }

    /// <summary>Tên đơn vị.</summary>
    string Name { get; }

    /// <summary>Đơn vị cha (null = nút gốc).</summary>
    Guid? ParentId { get; set; }

    /// <summary>
    /// Đường dẫn vật hóa từ gốc tới chính nút này: <c>/&lt;id gốc&gt;/…/&lt;id nút&gt;/</c> (Guid dạng chữ thường có gạch).
    /// Mọi nút con có <c>Path</c> bắt đầu bằng <c>Path</c> của nút cha.
    /// </summary>
    string Path { get; set; }
}

/// <summary>
/// Tổ chức Đảng (Đảng ủy, Đảng bộ bộ phận, Chi bộ…) — xếp thành cây theo <see cref="ParentId"/>.
/// Tên kỹ thuật giữ là "PartyCell" (hợp đồng API/phạm vi gán <c>PartyCell</c>); tên hiển thị: "Tổ chức Đảng".
/// </summary>
public class PartyCell : IAuditableEntity, ISoftDeletable, IOrgUnit
{
    /// <summary>Mã định danh tổ chức Đảng</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã ký hiệu (ví dụ: DU-ATTECH, CB-KT)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ của tổ chức Đảng</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả nhiệm vụ chính trị trọng tâm</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tổ chức Đảng cấp trên trực tiếp (null = gốc).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Loại đơn vị (Đảng ủy, Đảng bộ bộ phận, Chi bộ…) — danh mục <c>org_unit_types</c>, bên Đảng.</summary>
    public Guid? UnitTypeId { get; set; }

    /// <summary>Loại đơn vị (navigation).</summary>
    public OrgUnitType? UnitType { get; set; }

    /// <inheritdoc />
    public string Path { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị trong danh mục (nhỏ đứng trước)</summary>
    public int SortOrder { get; set; }

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm tạo trên hệ thống</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Cán bộ, Đảng viên sinh hoạt tại tổ chức Đảng này (nơi sinh hoạt Đảng).</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}

/// <summary>
/// Đơn vị chính quyền (Công ty, Phòng, Trung tâm, Xưởng, Đội…) — xếp thành cây theo <see cref="ParentId"/>.
/// Tên kỹ thuật giữ là "Department" (hợp đồng API/phạm vi gán <c>Department</c>); tên hiển thị: "Đơn vị chính quyền".
/// </summary>
public class AdministrativeDepartment : IAuditableEntity, ISoftDeletable, IOrgUnit
{
    /// <summary>Mã định danh đơn vị</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã ký hiệu (ví dụ: ATTECH, PH-KT)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên đơn vị</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả chức năng nhiệm vụ</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Đơn vị cấp trên trực tiếp (null = gốc).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Loại đơn vị (Công ty, Phòng, Trung tâm…) — danh mục <c>org_unit_types</c>, bên chính quyền.</summary>
    public Guid? UnitTypeId { get; set; }

    /// <summary>Loại đơn vị (navigation).</summary>
    public OrgUnitType? UnitType { get; set; }

    /// <inheritdoc />
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Thứ tự hiển thị trong danh mục (nhỏ đứng trước).
    /// Người đứng đầu đơn vị không lưu ở đây mà xác định bằng chức vụ (<see cref="MemberPosition"/>) và gán vai trò.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>Trạng thái hoạt động</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Cán bộ có đơn vị công tác chính là đơn vị này.</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}

/// <summary>
/// Danh mục loại đơn vị (bảng <c>org_unit_types</c>): Đảng ủy, Đảng bộ bộ phận, Chi bộ; Công ty, Phòng, Trung tâm, Xưởng, Đội…
/// Quản trị tự thêm/sửa; chỉ để phân loại và hiển thị, không mang luật phân quyền.
/// </summary>
public class OrgUnitType : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh loại đơn vị.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên loại (duy nhất trong cùng một bên).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bên áp dụng: tổ chức Đảng hay đơn vị chính quyền.</summary>
    public OrgSide Side { get; set; } = OrgSide.Administrative;

    /// <summary>Thứ tự hiển thị (nhỏ đứng trước).</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang dùng (false = ngừng dùng: không chọn được cho đơn vị mới).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
