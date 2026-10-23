using System;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Security;

/// <summary>Nguồn quyền duy nhất của mỗi request: tính từ CSDL (có cache), không đọc quyền trong JWT.</summary>
public interface IPermissionResolver
{
    /// <summary>Người dùng không tồn tại, đã xóa hoặc bị vô hiệu hóa → tập rỗng.</summary>
    Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Gọi khi cấu hình phân quyền thay đổi để có hiệu lực ngay ở request kế tiếp.</summary>
public interface IAccessCacheInvalidator
{
    /// <summary>Sửa bản gán vai trò, khóa/xóa/đổi mật khẩu người đó.</summary>
    void InvalidateUser(Guid userId);

    /// <summary>Sửa vai trò hoặc quyền của vai trò.</summary>
    void InvalidateAll();
}
