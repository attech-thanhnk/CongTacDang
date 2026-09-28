using System;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Nguồn quyền duy nhất của mỗi request: tính quyền hiệu lực của người dùng từ CSDL (có cache),
/// không đọc vai trò/quyền trong JWT.
/// </summary>
public interface IPermissionResolver
{
    /// <summary>
    /// Lấy tập quyền hiệu lực của người dùng. Người dùng không tồn tại, đã xóa hoặc bị vô hiệu hóa → tập rỗng.
    /// </summary>
    Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Xóa cache quyền khi cấu hình phân quyền thay đổi để thay đổi có hiệu lực ngay ở request kế tiếp.
/// </summary>
public interface IAccessCacheInvalidator
{
    /// <summary>Xóa cache của một người (sửa bản gán vai trò, khóa/xóa/đổi mật khẩu người đó).</summary>
    void InvalidateUser(Guid userId);

    /// <summary>Xóa toàn bộ cache (sửa vai trò hoặc quyền của vai trò).</summary>
    void InvalidateAll();
}
