namespace CongTacDang.Domain.Enums;

public enum LoginResult
{
    Success = 0,

    InvalidPassword = 1,

    /// <summary>Hoặc tài khoản đã xóa.</summary>
    UnknownUser = 2,

    /// <summary>Khóa tạm do nhập sai mật khẩu nhiều lần.</summary>
    LockedOut = 3,

    /// <summary>Quản trị đã vô hiệu hóa.</summary>
    Disabled = 4
}
