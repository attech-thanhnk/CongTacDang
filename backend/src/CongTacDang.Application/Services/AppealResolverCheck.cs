using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Kiểm tra kiến nghị còn người xử lý được hay không — cùng cách với kiểm tra kẹt luồng khi mở kỳ: ứng viên là tài khoản đang
/// hoạt động có vai trò chứa <c>evaluation.appeal.resolve</c>, quyền hiệu lực + phạm vi tính bằng resolver và guard; loại chủ hồ sơ,
/// người gửi và người có xung đột lợi ích của kiến nghị (người đã thực hiện bước bị kiến nghị).
/// Không đổi luật xử lý: kiến nghị vẫn được nhận, chỉ cảnh báo để quản trị gán thêm người xử lý.
/// </summary>
public sealed class AppealResolverCheck
{
    /// <summary>Cảnh báo hiển thị trên kiến nghị khi không còn ai xử lý được.</summary>
    public const string NoResolverWarning =
        "Hiện không có tài khoản nào (ngoài chủ hồ sơ và người đã thực hiện bước bị kiến nghị) có quyền \"Xử lý kiến nghị về kết quả\" "
        + "trong phạm vi hồ sơ này, nên kiến nghị chưa có người xử lý. Kiến nghị vẫn được ghi nhận; người quản lý gán vai trò đã được "
        + "nhắc gán vai trò có quyền này cho người phù hợp.";

    private readonly IEvaluationWorkflowRepository _workflowRepo;
    private readonly IPermissionResolver _permissions;
    private List<Guid>? _candidates;
    private readonly Dictionary<Guid, EffectivePermissions> _effective = new();

    public AppealResolverCheck(IEvaluationWorkflowRepository workflowRepo, IPermissionResolver permissions)
    {
        _workflowRepo = workflowRepo;
        _permissions = permissions;
    }

    /// <summary>Kiến nghị có ít nhất một người được xử lý (không tính người bị chặn do xung đột lợi ích).</summary>
    public async Task<bool> HasResolverAsync(EvaluationAppeal appeal, EvaluationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(appeal);
        ArgumentNullException.ThrowIfNull(record);
        _candidates ??= await _workflowRepo.ListActiveUserIdsWithAnyPermissionAsync(
            new[] { PermissionCodes.EvaluationAppealResolve }, DateTime.UtcNow, ct);

        var target = AccessTarget.ForRecord(record);
        foreach (var userId in _candidates)
        {
            if (userId == record.MemberId || userId == appeal.SubmittedById || appeal.ConflictedUserIds.Contains(userId))
                continue;
            if (!_effective.TryGetValue(userId, out var effective))
            {
                effective = await _permissions.GetAsync(userId, ct);
                _effective[userId] = effective;
            }
            if (AuthorizationGuard.Evaluate(effective, PermissionCodes.EvaluationAppealResolve, target))
                return true;
        }
        return false;
    }
}
