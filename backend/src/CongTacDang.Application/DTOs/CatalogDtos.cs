namespace CongTacDang.Application.DTOs;

/// <summary>Cập nhật: trường null giữ nguyên.</summary>
public class SaveCatalogItemDto
{
    /// <summary>Duy nhất, không phân biệt hoa thường (chuẩn hóa chữ hoa).
    /// Thêm Chi bộ để trống → tự sinh; thêm Phòng bắt buộc nhập.</summary>
    public string? Code { get; set; }

    /// <summary>Tên viết tắt (tối đa 30 ký tự), dùng đặt tên tệp xuất. Cập nhật: null = giữ nguyên, rỗng = xóa.</summary>
    public string? ShortName { get; set; }

    /// <summary>Bắt buộc khi thêm mới.</summary>
    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Nhỏ đứng trước.</summary>
    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>Cùng bên. Thêm: null = gốc. Cập nhật: null = giữ nguyên, <c>Guid.Empty</c> = chuyển thành gốc.
    /// Không được chọn chính nó hoặc đơn vị con cháu (tạo vòng).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Đúng bên. Cập nhật: null = giữ nguyên, <c>Guid.Empty</c> = bỏ loại.</summary>
    public Guid? UnitTypeId { get; set; }
}
