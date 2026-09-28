using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Bảng ánh xạ mã quyền cũ (<see cref="AppPermissions"/>) sang mã quyền mới (<see cref="PermissionCodes"/>)
/// theo docs/thiet-ke/phan-quyen.md mục 3. Chỉ dùng trong giai đoạn chuyển tiếp (adapter v0, chuyển dữ liệu);
/// task 09 xóa cùng với <see cref="AppPermissions"/>.
/// </summary>
[Obsolete("Chỉ dùng cho policy chuyển tiếp của endpoint còn khai báo mã cũ (ngoài phạm vi task 09).")]
public static class LegacyPermissionMap
{
    /// <summary>
    /// Mã cũ → các mã mới tương đương. Mã cũ không còn tương đương (<c>branches.read</c>, <c>attachments.read</c>)
    /// ánh xạ sang danh sách rỗng. <c>attachments.upload/delete</c> chỉ ánh xạ sang <c>evaluation.self</c>
    /// (tệp của mình); <c>attachment.general.manage</c> được cấp riêng cho vai trò quản trị.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> OldToNew = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AppPermissions.UsersRead] = new[] { PermissionCodes.SystemUsersRead },
        [AppPermissions.UsersCreate] = new[] { PermissionCodes.SystemUsersManage },
        [AppPermissions.UsersUpdate] = new[] { PermissionCodes.SystemUsersManage },
        [AppPermissions.UsersDelete] = new[] { PermissionCodes.SystemUsersManage },
        [AppPermissions.BranchesRead] = Array.Empty<string>(),
        [AppPermissions.BranchesCreate] = new[] { PermissionCodes.CatalogManage },
        [AppPermissions.BranchesUpdate] = new[] { PermissionCodes.CatalogManage },
        [AppPermissions.BranchesDelete] = new[] { PermissionCodes.CatalogManage },
        [AppPermissions.AttachmentsRead] = Array.Empty<string>(),
        [AppPermissions.AttachmentsUpload] = new[] { PermissionCodes.EvaluationSelf },
        [AppPermissions.AttachmentsDelete] = new[] { PermissionCodes.EvaluationSelf },
        [AppPermissions.EvaluationsRead] = new[] { PermissionCodes.EvaluationRead },
        [AppPermissions.EvaluationsRegister] = new[] { PermissionCodes.EvaluationSelf },
        [AppPermissions.EvaluationsSelfScore] = new[] { PermissionCodes.EvaluationSelf },
        [AppPermissions.EvaluationsBranchVote] = new[]
        {
            PermissionCodes.EvaluationCellConfirm, PermissionCodes.CollectiveManage,
            PermissionCodes.MeetingRead, PermissionCodes.MeetingManage
        },
        [AppPermissions.EvaluationsAppraise] = new[] { PermissionCodes.EvaluationAppraise },
        [AppPermissions.EvaluationsApprove] = new[] { PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationDecideExternal },
        [AppPermissions.ReportsExport] = new[] { PermissionCodes.ReportExport },
        [AppPermissions.RolesManage] = new[]
        {
            PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage, PermissionCodes.SystemAuditRead
        },
    };

    /// <summary>Các mã cũ có ánh xạ sang <paramref name="newCode"/>.</summary>
    public static IEnumerable<string> LegacyCodesFor(string newCode) =>
        OldToNew.Where(pair => pair.Value.Contains(newCode, StringComparer.Ordinal)).Select(pair => pair.Key);
}
