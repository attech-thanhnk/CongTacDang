namespace CongTacDang.Application.DTOs;

/// <summary>Mô tả một cột của file mẫu (để giao diện dựng bảng xem trước động).</summary>
public class ImportColumnDto
{
    /// <summary>Khóa trong <see cref="ImportPreviewRowDto.Data"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Tiêu đề cột trong file Excel.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Bắt buộc có giá trị.</summary>
    public bool Required { get; set; }

    /// <summary>Hướng dẫn nhập.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Giá trị hợp lệ (null = tự do).</summary>
    public List<string>? AllowedValues { get; set; }
}

/// <summary>Một loại dữ liệu người dùng hiện tại được phép nhập.</summary>
public class ImportKindDto
{
    /// <summary>Mã loại (dùng trong URL).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Các cột của file mẫu.</summary>
    public List<ImportColumnDto> Columns { get; set; } = new();
}

/// <summary>Kết quả phân tích một dòng ở bước xem trước.</summary>
public class ImportPreviewRowDto
{
    /// <summary>Số dòng trong Excel (dòng tiêu đề là 1).</summary>
    public int RowNumber { get; set; }

    /// <summary><c>create</c> | <c>update</c> | <c>error</c>.</summary>
    public string Action { get; set; } = "error";

    /// <summary>Lý do lỗi (rỗng nếu hợp lệ).</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Giá trị ô theo khóa cột.</summary>
    public Dictionary<string, string> Data { get; set; } = new();
}

/// <summary>Tóm tắt bước xem trước.</summary>
public class ImportSummaryDto
{
    /// <summary>Tổng số dòng dữ liệu.</summary>
    public int Total { get; set; }

    /// <summary>Số dòng sẽ tạo mới.</summary>
    public int Create { get; set; }

    /// <summary>Số dòng sẽ cập nhật.</summary>
    public int Update { get; set; }

    /// <summary>Số dòng lỗi.</summary>
    public int Error { get; set; }
}

/// <summary>Kết quả bước xem trước.</summary>
public class ImportPreviewDto
{
    /// <summary>Id phiên — dùng cho bước xác nhận (chỉ người tải lên được xác nhận, hết hạn sau 30 phút).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Mã loại import.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tên tệp đã tải lên.</summary>
    public string? FileName { get; set; }

    /// <summary>Thời điểm phiên hết hạn (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Được xác nhận ghi dữ liệu (không có dòng lỗi và có ít nhất một dòng).</summary>
    public bool CanCommit { get; set; }

    /// <summary>Các cột để dựng bảng.</summary>
    public List<ImportColumnDto> Columns { get; set; } = new();

    /// <summary>Từng dòng.</summary>
    public List<ImportPreviewRowDto> Rows { get; set; } = new();

    /// <summary>Tóm tắt.</summary>
    public ImportSummaryDto Summary { get; set; } = new();
}

/// <summary>Kết quả bước xác nhận.</summary>
public class ImportCommitResultDto
{
    /// <summary>Số bản ghi tạo mới.</summary>
    public int Created { get; set; }

    /// <summary>Số bản ghi cập nhật.</summary>
    public int Updated { get; set; }

    /// <summary>Mã tải tệp kết quả (chỉ tải được một lần), null nếu loại import không có tệp kết quả.</summary>
    public string? ResultFileToken { get; set; }

    /// <summary>Thời điểm tệp kết quả hết hạn (UTC).</summary>
    public DateTimeOffset? ResultFileExpiresAt { get; set; }
}
