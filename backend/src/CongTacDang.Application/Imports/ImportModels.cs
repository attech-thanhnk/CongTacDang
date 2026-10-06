using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;

namespace CongTacDang.Application.Imports;

/// <summary>Giới hạn chung của chức năng nhập dữ liệu từ Excel.</summary>
public static class ImportLimits
{
    /// <summary>Dung lượng tối đa của file tải lên (5 MB).</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Số dòng dữ liệu tối đa trong một file (không tính dòng tiêu đề, dòng trống).</summary>
    public const int MaxRows = 2000;

    /// <summary>Thời gian giữ phiên xem trước và file kết quả.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Độ dài tối đa của một ô (ký tự).</summary>
    public const int MaxCellLength = 1000;

    /// <summary>Tên sheet dữ liệu của file mẫu.</summary>
    public const string DataSheetName = "Dữ liệu";

    /// <summary>Tên sheet hướng dẫn của file mẫu.</summary>
    public const string GuideSheetName = "Hướng dẫn";

    /// <summary>Kiểu nội dung file .xlsx.</summary>
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>
/// Một cột của file nhập.
/// </summary>
/// <param name="Key">Khóa kỹ thuật (dùng trong dữ liệu xem trước).</param>
/// <param name="Header">Tiêu đề cột trong file (khớp không phân biệt hoa thường, bỏ dấu * cuối).</param>
/// <param name="Required">Bắt buộc nhập.</param>
/// <param name="Description">Mô tả cách nhập (hiện trong sheet Hướng dẫn).</param>
/// <param name="AllowedValues">Danh sách giá trị hợp lệ (tạo danh sách chọn trong file mẫu); null = tự do.</param>
/// <param name="MultipleValues">Nhiều giá trị phân tách bằng dấu ";" (không tạo danh sách chọn, chỉ liệt kê trong Hướng dẫn).</param>
/// <param name="Example">Ví dụ giá trị.</param>
public sealed record ImportColumn(
    string Key,
    string Header,
    bool Required,
    string Description,
    IReadOnlyList<string>? AllowedValues = null,
    bool MultipleValues = false,
    string? Example = null)
{
    /// <summary>Bản sao kèm danh sách giá trị hợp lệ (lấy từ CSDL khi dựng file mẫu).</summary>
    public ImportColumn WithAllowedValues(IReadOnlyList<string> values) => this with { AllowedValues = values };
}

/// <summary>Một dòng dữ liệu đọc từ file (đã cắt khoảng trắng; ô trống = chuỗi rỗng).</summary>
public sealed class ImportRawRow
{
    /// <summary>Khởi tạo.</summary>
    public ImportRawRow(int rowNumber, IReadOnlyDictionary<string, string> values)
    {
        RowNumber = rowNumber;
        Values = values;
    }

    /// <summary>Số dòng trong file Excel (dòng tiêu đề là 1).</summary>
    public int RowNumber { get; }

    /// <summary>Giá trị theo khóa cột.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Giá trị của cột (chuỗi rỗng nếu không có).</summary>
    public string Get(string key) => Values.TryGetValue(key, out var value) ? value : string.Empty;
}

/// <summary>Kết quả xem trước của một dòng.</summary>
public enum ImportRowAction
{
    /// <summary>Tạo mới.</summary>
    Create,

    /// <summary>Cập nhật bản ghi đã có.</summary>
    Update,

    /// <summary>Có lỗi, không nhập được.</summary>
    Error
}

/// <summary>Kết quả phân tích một dòng: hành động dự kiến và lỗi (nếu có).</summary>
public sealed class ImportRowResult
{
    /// <summary>Khởi tạo từ dòng thô.</summary>
    public ImportRowResult(ImportRawRow raw) => Raw = raw;

    /// <summary>Dòng thô.</summary>
    public ImportRawRow Raw { get; }

    /// <summary>Số dòng trong file.</summary>
    public int RowNumber => Raw.RowNumber;

    /// <summary>Hành động dự kiến (Error khi có lỗi).</summary>
    public ImportRowAction Action { get; set; } = ImportRowAction.Create;

    /// <summary>Danh sách lỗi của dòng.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Ghi chú thêm (không phải lỗi), ví dụ "Giữ nguyên mật khẩu".</summary>
    public List<string> Notes { get; } = new();

    /// <summary>Dòng hợp lệ.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Thêm lỗi (bỏ trùng).</summary>
    public void AddError(string message)
    {
        if (!Errors.Contains(message))
            Errors.Add(message);
    }
}

/// <summary>Kết quả phân tích toàn bộ file.</summary>
public sealed class ImportAnalysis
{
    /// <summary>Khởi tạo.</summary>
    public ImportAnalysis(IReadOnlyList<ImportRowResult> rows, object? payload)
    {
        Rows = rows;
        Payload = payload;
    }

    /// <summary>Các dòng theo thứ tự trong file.</summary>
    public IReadOnlyList<ImportRowResult> Rows { get; }

    /// <summary>Dữ liệu đã phân tích (kiểu riêng của từng loại nhập) dùng khi ghi.</summary>
    public object? Payload { get; }

    /// <summary>Có dòng lỗi.</summary>
    public bool HasErrors => Rows.Any(r => !r.IsValid);
}

/// <summary>Tài khoản mới tạo kèm mật khẩu tạm (chỉ giữ trong bộ nhớ để xuất file một lần).</summary>
public sealed record ImportCredential(string Username, string FullName, string TemporaryPassword);

/// <summary>Kết quả ghi dữ liệu của một loại nhập.</summary>
public sealed class ImportCommitOutcome
{
    /// <summary>Số bản ghi tạo mới.</summary>
    public int Created { get; set; }

    /// <summary>Số bản ghi cập nhật.</summary>
    public int Updated { get; set; }

    /// <summary>Mật khẩu tạm của tài khoản mới (loại nhập cán bộ).</summary>
    public List<ImportCredential> Credentials { get; } = new();
}

/// <summary>Lỗi khi ghi một dòng — giữ mã trạng thái HTTP của lỗi gốc, thêm số dòng vào thông báo.</summary>
public sealed class ImportRowException : AppException
{
    /// <summary>Khởi tạo.</summary>
    public ImportRowException(int rowNumber, AppException inner)
        : base($"Dòng {rowNumber}: {inner.Message}", inner.StatusCode)
    {
    }
}

/// <summary>Một file tải về.</summary>
/// <param name="Content">Nội dung.</param>
/// <param name="ContentType">Kiểu nội dung.</param>
/// <param name="FileName">Tên file.</param>
public sealed record ImportFile(byte[] Content, string ContentType, string FileName);

#region DTO

/// <summary>Một loại nhập người dùng hiện tại được dùng (<c>GET /api/imports/kinds</c>).</summary>
public sealed class ImportKindDto
{
    /// <summary>Mã loại (dùng trong đường dẫn API).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Các cột của file.</summary>
    public List<ImportColumnDto> Columns { get; set; } = new();
}

/// <summary>Cột của file nhập.</summary>
public sealed class ImportColumnDto
{
    /// <summary>Khóa kỹ thuật.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Tiêu đề cột.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Bắt buộc.</summary>
    public bool Required { get; set; }

    /// <summary>Mô tả.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>Kết quả xem trước (<c>POST /api/imports/{kind}/preview</c>).</summary>
public sealed class ImportPreviewDto
{
    /// <summary>Mã phiên dùng để xác nhận.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Loại nhập.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tên file đã tải lên.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Hết hạn phiên (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Các cột (để dựng bảng xem trước).</summary>
    public List<ImportColumnDto> Columns { get; set; } = new();

    /// <summary>Các dòng.</summary>
    public List<ImportPreviewRowDto> Rows { get; set; } = new();

    /// <summary>Tổng hợp.</summary>
    public ImportSummaryDto Summary { get; set; } = new();
}

/// <summary>Một dòng xem trước.</summary>
public sealed class ImportPreviewRowDto
{
    /// <summary>Số dòng trong file.</summary>
    public int RowNumber { get; set; }

    /// <summary><c>create</c> | <c>update</c> | <c>error</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Lỗi của dòng.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Ghi chú (không phải lỗi).</summary>
    public List<string> Notes { get; set; } = new();

    /// <summary>Giá trị theo khóa cột.</summary>
    public Dictionary<string, string> Data { get; set; } = new();
}

/// <summary>Tổng hợp xem trước.</summary>
public sealed class ImportSummaryDto
{
    /// <summary>Tổng số dòng.</summary>
    public int Total { get; set; }

    /// <summary>Số dòng tạo mới.</summary>
    public int Create { get; set; }

    /// <summary>Số dòng cập nhật.</summary>
    public int Update { get; set; }

    /// <summary>Số dòng lỗi.</summary>
    public int Error { get; set; }
}

/// <summary>Kết quả xác nhận nhập (<c>POST /api/imports/sessions/{id}/commit</c>).</summary>
public sealed class ImportCommitResultDto
{
    /// <summary>Loại nhập.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Tổng số dòng đã ghi.</summary>
    public int Total { get; set; }

    /// <summary>Số bản ghi tạo mới.</summary>
    public int Created { get; set; }

    /// <summary>Số bản ghi cập nhật.</summary>
    public int Updated { get; set; }

    /// <summary>Mã tải file mật khẩu tạm (một lần); null khi không có tài khoản mới.</summary>
    public string? ResultFileToken { get; set; }

    /// <summary>Hết hạn file mật khẩu tạm (UTC).</summary>
    public DateTime? ResultFileExpiresAt { get; set; }
}

#endregion
