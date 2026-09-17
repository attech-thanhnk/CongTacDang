using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể lưu trữ Refresh Token để làm mới Access Token (Token Rotation) theo chuẩn OAuth2
/// </summary>
public class RefreshToken
{
    /// <summary>Mã định danh bản ghi token</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã định danh cán bộ sở hữu token</summary>
    public Guid UserId { get; set; }

    /// <summary>Đối tượng cán bộ sở hữu</summary>
    public PartyMemberProfile User { get; set; } = null!;

    /// <summary>Chuỗi giá trị token ngẫu nhiên bảo mật cao</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Thời điểm hết hạn của Refresh Token</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Trạng thái bị thu hồi / vô hiệu hóa</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Thời điểm khởi tạo token</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Token mới thay thế (dùng để phát hiện tấn công tái sử dụng token - Token Reuse Detection)</summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>Địa chỉ IP của thiết bị khi yêu cầu cấp token</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Kiểm tra token đã hết hạn theo thời gian chưa</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Kiểm tra token còn hợp lệ sử dụng không</summary>
    public bool IsActive => !IsRevoked && !IsExpired;
}
