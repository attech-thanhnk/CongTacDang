namespace CongTacDang.Domain.Enums;

/// <summary>Kết quả một lần đăng nhập, ghi vào nhật ký đăng nhập.</summary>
public enum LoginResult
{
    /// <summary>Đăng nhập thành công.</summary>
    Success = 0,

    /// <summary>Sai mật khẩu.</summary>
    InvalidPassword = 1,

    /// <summary>Tên đăng nhập không tồn tại (hoặc tài khoản đã xóa).</summary>
    UnknownUser = 2,

    /// <summary>Tài khoản đang bị khóa tạm thời do nhập sai mật khẩu nhiều lần.</summary>
    LockedOut = 3,

    /// <summary>Tài khoản đã bị quản trị khóa (vô hiệu hóa).</summary>
    Disabled = 4
}
