using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thông tin định danh người tạo/cập nhật được dùng chung cho các entity nghiệp vụ.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Hợp đồng xóa mềm. Bản ghi vẫn tồn tại để phục vụ khôi phục và audit.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Phiên bản bản ghi cho optimistic concurrency (ánh xạ cột hệ thống xmin của PostgreSQL).
/// Là thuộc tính thật nên đọc được cả với truy vấn AsNoTracking.
/// </summary>
public interface IVersioned
{
    uint Version { get; set; }
}
