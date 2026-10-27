using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Nút trong cây đơn vị (tổ chức Đảng hoặc đơn vị chính quyền).</summary>
public interface IOrgUnit
{
    Guid Id { get; }

    string Code { get; }

    string Name { get; }

    /// <summary>Tên viết tắt dùng đặt tên tệp xuất; null = chưa nhập.</summary>
    string? ShortName { get; }

    /// <summary>Null = nút gốc.</summary>
    Guid? ParentId { get; set; }

    /// <summary>Đường dẫn từ gốc: <c>/&lt;id gốc&gt;/…/&lt;id nút&gt;/</c> (Guid chữ thường); nút con bắt đầu bằng Path của cha.</summary>
    string Path { get; set; }
}

/// <summary>Tổ chức Đảng (Đảng ủy, Đảng bộ bộ phận, Chi bộ…). Tên kỹ thuật giữ "PartyCell" vì hợp đồng API/phạm vi gán.</summary>
public class PartyCell : IAuditableEntity, ISoftDeletable, IOrgUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ví dụ DU-CT, CB-KT.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên viết tắt dùng đặt tên tệp xuất; null = dùng mã.</summary>
    public string? ShortName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Null = gốc.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Danh mục <c>org_unit_types</c>, bên Đảng.</summary>
    public Guid? UnitTypeId { get; set; }

    public OrgUnitType? UnitType { get; set; }

    /// <inheritdoc />
    public string Path { get; set; } = string.Empty;

    /// <summary>Nhỏ đứng trước.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Đảng viên sinh hoạt tại tổ chức Đảng này.</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}

/// <summary>Đơn vị chính quyền (Công ty, Phòng, Trung tâm…). Tên kỹ thuật giữ "Department" vì hợp đồng API/phạm vi gán.</summary>
public class AdministrativeDepartment : IAuditableEntity, ISoftDeletable, IOrgUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ví dụ CT, PH-KT.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên viết tắt dùng đặt tên tệp xuất; null = dùng mã.</summary>
    public string? ShortName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Null = gốc.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Danh mục <c>org_unit_types</c>, bên chính quyền.</summary>
    public Guid? UnitTypeId { get; set; }

    public OrgUnitType? UnitType { get; set; }

    /// <inheritdoc />
    public string Path { get; set; } = string.Empty;

    /// <summary>Nhỏ đứng trước. Người đứng đầu không lưu ở đây mà xác định qua chức vụ (<see cref="MemberPosition"/>).</summary>
    public int SortOrder { get; set; }

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

/// <summary>Danh mục loại đơn vị; chỉ để phân loại và hiển thị, không mang luật phân quyền.</summary>
public class OrgUnitType : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Duy nhất trong cùng một bên.</summary>
    public string Name { get; set; } = string.Empty;

    public OrgSide Side { get; set; } = OrgSide.Administrative;

    /// <summary>Nhỏ đứng trước.</summary>
    public int SortOrder { get; set; }

    /// <summary>False = ngừng dùng, không chọn được cho đơn vị mới.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Ngày nghỉ ngoài thứ Bảy, Chủ nhật; dùng khi tính thời hạn theo ngày làm việc.</summary>
public class Holiday : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Duy nhất trong danh mục.</summary>
    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Quy ước chung cho tên viết tắt của đơn vị / tổ chức Đảng.</summary>
public static class CatalogShortName
{
    public const int MaxLength = 30;
}
