using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>Đơn vị của biên bản/phiếu lập cho một nhóm hồ sơ (có thể thuộc nhiều đơn vị).</summary>
internal static class RecordGroupUnits
{
    /// <summary>
    /// Lấy đơn vị chính quyền và tổ chức Đảng chung gần nhất của các hồ sơ; giữ bên nào người lập có quyền ghi,
    /// ưu tiên đủ cả hai bên. Không bên nào được phép thì báo 403 theo cặp chung đầy đủ.
    /// </summary>
    public static (Guid? DepartmentId, Guid? PartyCellId) Resolve(IAuthorizationGuard guard, string permission,
        IReadOnlyCollection<EvaluationRecord> records, string action)
    {
        var departmentId = Common(records.Select(r => r.DepartmentId), records.Select(r => r.Department?.Path));
        var partyCellId = Common(records.Select(r => r.PartyCellId), records.Select(r => r.PartyCell?.Path));
        if (departmentId is null && partyCellId is null)
            throw new ValidationException($"{action} không thành công: không xác định được đơn vị chung của các hồ sơ đã chọn.");

        var options = new (Guid? Department, Guid? PartyCell)[] { (departmentId, partyCellId), (departmentId, null), (null, partyCellId) };
        foreach (var (d, c) in options)
            if ((d is not null || c is not null) && guard.Can(permission, AccessTarget.ForUnitsWrite(d, c)))
                return (d, c);
        guard.Ensure(permission, AccessTarget.ForUnitsWrite(departmentId, partyCellId));
        return (departmentId, partyCellId);
    }

    private static Guid? Common(IEnumerable<Guid?> ids, IEnumerable<string?> paths)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 1)
            return distinct[0];
        // Hồ sơ khác đơn vị: lấy tổ tiên chung gần nhất theo đường dẫn cây.
        return distinct.Any(i => i is null) ? null : OrgTree.CommonAncestor(paths);
    }
}
