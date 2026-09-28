using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CongTacDang.Application.Common.Models;

/// <summary>
/// Cấu trúc phản hồi chuẩn hóa của toàn bộ Web API
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của payload trả về</typeparam>
public class ApiResponse<T>
{
    /// <summary>Trạng thái thành công hay thất bại</summary>
    public bool Success { get; set; }

    /// <summary>Thông điệp phản hồi từ máy chủ</summary>
    public string? Message { get; set; }

    /// <summary>Dữ liệu kết quả nghiệp vụ</summary>
    public T? Data { get; set; }

    /// <summary>Danh sách lỗi chi tiết (nếu có)</summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Mã lỗi máy đọc được để giao diện xử lý riêng (ví dụ <c>PASSWORD_CHANGE_REQUIRED</c>).
    /// Không có mã → không xuất hiện trong JSON.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    /// <summary>Thời điểm phản hồi theo chuẩn UTC</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Khởi tạo phản hồi thành công kèm dữ liệu</summary>
    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    /// <summary>Khởi tạo phản hồi thất bại kèm thông báo lỗi</summary>
    public static ApiResponse<T> Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };
}

/// <summary>
/// Cấu trúc phản hồi chuẩn hóa không kèm dữ liệu (void)
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>Khởi tạo phản hồi thành công</summary>
    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    /// <summary>Khởi tạo phản hồi thất bại kèm thông điệp</summary>
    public static new ApiResponse Fail(string message, List<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };

    /// <summary>Khởi tạo phản hồi thất bại kèm mã lỗi máy đọc được.</summary>
    public static ApiResponse FailWithCode(string code, string message) => new()
    {
        Success = false,
        Message = message,
        Code = code
    };
}
