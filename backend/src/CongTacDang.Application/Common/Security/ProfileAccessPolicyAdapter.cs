using System;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Security;

/// <summary>Thao tác trên hồ sơ tài khoản (chỉ còn dùng cho <see cref="IAccessPolicy"/> tương thích).</summary>
[Obsolete("Dùng IAuthorizationGuard với mã quyền (PermissionCodes).")]
public enum AccessOperation
{
    /// <summary>Xem.</summary>
    Read,

    /// <summary>Cập nhật.</summary>
    Update,

    /// <summary>Xóa.</summary>
    Delete,

    /// <summary>Kết xuất.</summary>
    Export,

    /// <summary>Không còn dùng.</summary>
    BranchReview,

    /// <summary>Không còn dùng.</summary>
    Approve
}

/// <summary>
/// Lớp tương thích cho code ngoài phạm vi task 09 (<c>UserService</c> — task 08) còn gọi <c>CanAccessProfile</c>.
/// Luật cũ theo tên vai trò đã bị xóa; lớp này chuyển sang <see cref="IAuthorizationGuard"/>:
/// xem hồ sơ người khác cần <c>system.users.read</c>, sửa/xóa cần <c>system.users.manage</c>, trong phạm vi Phòng/Chi bộ của hồ sơ.
/// Chỉ đánh giá cho người dùng của request hiện tại (<paramref name="user"/> khác người đăng nhập → từ chối).
/// </summary>
[Obsolete("Dùng IAuthorizationGuard với PermissionCodes.SystemUsersRead/SystemUsersManage. Sẽ xóa khi task 08 bỏ chỗ dùng.")]
public interface IAccessPolicy
{
    /// <summary>Kiểm tra quyền trên hồ sơ người dùng của người khác hoặc của chính mình.</summary>
    bool CanAccessProfile(PartyMemberProfile user, PartyMemberProfile target, AccessOperation operation);
}

/// <summary>Triển khai <see cref="IAccessPolicy"/> trên <see cref="IAuthorizationGuard"/>. Đăng ký scoped.</summary>
#pragma warning disable CS0618
[Obsolete("Dùng IAuthorizationGuard.")]
public sealed class ProfileAccessPolicyAdapter : IAccessPolicy
{
    private readonly IAuthorizationGuard _guard;
    private readonly Interfaces.ICurrentUserService _currentUser;

    /// <summary>Khởi tạo adapter.</summary>
    public ProfileAccessPolicyAdapter(IAuthorizationGuard guard, Interfaces.ICurrentUserService currentUser)
    {
        _guard = guard;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public bool CanAccessProfile(PartyMemberProfile user, PartyMemberProfile target, AccessOperation operation)
    {
        if (user.Id == target.Id)
            return operation == AccessOperation.Read;

        if (_currentUser.UserId != user.Id)
            return false;

        var accessTarget = new AccessTarget(target.Id, target.DepartmentId, target.PartyCellId);
        return operation switch
        {
            AccessOperation.Read => _guard.Can(PermissionCodes.SystemUsersRead, accessTarget),
            AccessOperation.Update or AccessOperation.Delete => _guard.Can(PermissionCodes.SystemUsersManage, accessTarget),
            _ => false
        };
    }
}
#pragma warning restore CS0618
