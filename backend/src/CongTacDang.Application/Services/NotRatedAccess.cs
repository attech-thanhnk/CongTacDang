using System;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Cùng điều kiện với thao tác đánh dấu / bỏ đánh dấu Chưa xếp loại, dùng để trả nút cho giao diện.</summary>
public static class NotRatedAccess
{
    /// <summary><c>period.manage</c> trên hồ sơ, không phải hồ sơ của chính mình, trạng thái cho phép.</summary>
    public static bool Can(IAuthorizationGuard guard, Guid? currentUserId, EvaluationRecord record, PeriodStatus periodStatus, bool mark)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(record);
        if (currentUserId is { } userId && record.MemberId == userId)
            return false;
        var block = mark
            ? NotRatedRules.MarkBlockReason(record, periodStatus)
            : NotRatedRules.UnmarkBlockReason(record, periodStatus);
        return block == null && guard.Can(PermissionCodes.PeriodManage, AccessTarget.ForRecord(record));
    }
}
