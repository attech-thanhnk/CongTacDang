using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;

namespace CongTacDang.Application.Imports;

/// <summary>Giới hạn chung của chức năng nhập dữ liệu từ Excel.</summary>
public static class ImportLimits
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>Không tính dòng tiêu đề và dòng trống.</summary>
    public const int MaxRows = 2000;

    /// <summary>Thời gian giữ phiên xem trước và file kết quả.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Độ dài tối đa của một ô (ký tự).</summary>
    public const int MaxCellLength = 1000;

    public const string DataSheetName = "Dữ liệu";

    public const string GuideSheetName = "Hướng dẫn";

    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>Một cột của file nhập; tiêu đề khớp không phân biệt hoa thường, bỏ dấu * cuối.</summary>
/// <param name="AllowedValues">Tạo danh sách chọn trong file mẫu; null = tự do.</param>
/// <param name="MultipleValues">Nhiều giá trị phân tách bằng ";", không tạo danh sách chọn.</param>
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
    public ImportRawRow(int rowNumber, IReadOnlyDictionary<string, string> values)
    {
        RowNumber = rowNumber;
        Values = values;
    }

    /// <summary>Số dòng trong file Excel (dòng tiêu đề là 1).</summary>
    public int RowNumber { get; }

    public IReadOnlyDictionary<string, string> Values { get; }

    public string Get(string key) => Values.TryGetValue(key, out var value) ? value : string.Empty;
}

/// <summary>Kết quả xem trước của một dòng.</summary>
public enum ImportRowAction
{
    Create,

    Update,

    Error
}

/// <summary>Kết quả phân tích một dòng: hành động dự kiến và lỗi (nếu có).</summary>
public sealed class ImportRowResult
{
    public ImportRowResult(ImportRawRow raw) => Raw = raw;

    public ImportRawRow Raw { get; }

    public int RowNumber => Raw.RowNumber;

    /// <summary>Hành động dự kiến (Error khi có lỗi).</summary>
    public ImportRowAction Action { get; set; } = ImportRowAction.Create;

    public List<string> Errors { get; } = new();

    /// <summary>Ghi chú thêm (không phải lỗi), ví dụ "Giữ nguyên mật khẩu".</summary>
    public List<string> Notes { get; } = new();

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
    public ImportAnalysis(IReadOnlyList<ImportRowResult> rows, object? payload)
    {
        Rows = rows;
        Payload = payload;
    }

    public IReadOnlyList<ImportRowResult> Rows { get; }

    /// <summary>Dữ liệu đã phân tích (kiểu riêng của từng loại nhập) dùng khi ghi.</summary>
    public object? Payload { get; }

    public bool HasErrors => Rows.Any(r => !r.IsValid);
}

/// <summary>Tài khoản mới tạo kèm mật khẩu tạm (chỉ giữ trong bộ nhớ để xuất file một lần).</summary>
public sealed record ImportCredential(string Username, string FullName, string TemporaryPassword);

/// <summary>Kết quả ghi dữ liệu của một loại nhập.</summary>
public sealed class ImportCommitOutcome
{
    public int Created { get; set; }

    public int Updated { get; set; }

    /// <summary>Mật khẩu tạm của tài khoản mới (loại nhập cán bộ).</summary>
    public List<ImportCredential> Credentials { get; } = new();
}

/// <summary>Lỗi khi ghi một dòng — giữ mã trạng thái HTTP của lỗi gốc, thêm số dòng vào thông báo.</summary>
public sealed class ImportRowException : AppException
{
    public ImportRowException(int rowNumber, AppException inner)
        : base($"Dòng {rowNumber}: {inner.Message}", inner.StatusCode)
    {
    }
}

public sealed record ImportFile(byte[] Content, string ContentType, string FileName);

#region DTO

/// <summary>Một loại nhập người dùng hiện tại được dùng (<c>GET /api/imports/kinds</c>).</summary>
public sealed class ImportKindDto
{
    /// <summary>Mã loại (dùng trong đường dẫn API).</summary>
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<ImportColumnDto> Columns { get; set; } = new();
}

public sealed class ImportColumnDto
{
    public string Key { get; set; } = string.Empty;

    public string Header { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string Description { get; set; } = string.Empty;
}

/// <summary>Kết quả xem trước (<c>POST /api/imports/{kind}/preview</c>).</summary>
public sealed class ImportPreviewDto
{
    /// <summary>Mã phiên dùng để xác nhận.</summary>
    public Guid SessionId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>Hết hạn phiên (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    public List<ImportColumnDto> Columns { get; set; } = new();

    public List<ImportPreviewRowDto> Rows { get; set; } = new();

    public ImportSummaryDto Summary { get; set; } = new();
}

public sealed class ImportPreviewRowDto
{
    public int RowNumber { get; set; }

    /// <summary><c>create</c> | <c>update</c> | <c>error</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public List<string> Errors { get; set; } = new();

    /// <summary>Ghi chú (không phải lỗi).</summary>
    public List<string> Notes { get; set; } = new();

    public Dictionary<string, string> Data { get; set; } = new();
}

public sealed class ImportSummaryDto
{
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
    public string Kind { get; set; } = string.Empty;

    public int Total { get; set; }

    public int Created { get; set; }

    public int Updated { get; set; }

    /// <summary>Mã tải file mật khẩu tạm (một lần); null khi không có tài khoản mới.</summary>
    public string? ResultFileToken { get; set; }

    /// <summary>Hết hạn file mật khẩu tạm (UTC).</summary>
    public DateTime? ResultFileExpiresAt { get; set; }
}

#endregion
