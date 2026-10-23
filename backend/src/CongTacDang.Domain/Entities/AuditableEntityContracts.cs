using System;

namespace CongTacDang.Domain.Entities;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Xóa mềm: bản ghi vẫn còn để khôi phục và audit.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>Optimistic concurrency theo cột xmin; là thuộc tính thật nên đọc được cả khi AsNoTracking.</summary>
public interface IVersioned
{
    uint Version { get; set; }
}
