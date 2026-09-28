namespace CongTacDang.Application.DTOs;

/// <summary>
/// Dữ liệu thêm mới / cập nhật một mục danh mục (Phòng/đơn vị hoặc Chi bộ).
/// Khi cập nhật, trường để null được giữ nguyên giá trị hiện tại.
/// </summary>
public class SaveCatalogItemDto
{
    /// <summary>Mã ký hiệu (duy nhất, không phân biệt hoa thường; được chuẩn hóa thành chữ hoa).
    /// Thêm mới Chi bộ mà để trống → hệ thống tự sinh; thêm mới Phòng bắt buộc nhập.</summary>
    public string? Code { get; set; }

    /// <summary>Tên đầy đủ (bắt buộc khi thêm mới).</summary>
    public string? Name { get; set; }

    /// <summary>Mô tả chức năng, nhiệm vụ.</summary>
    public string? Description { get; set; }

    /// <summary>Thứ tự hiển thị (nhỏ đứng trước).</summary>
    public int? SortOrder { get; set; }

    /// <summary>Đang hoạt động; false = ngừng hoạt động.</summary>
    public bool? IsActive { get; set; }
}
