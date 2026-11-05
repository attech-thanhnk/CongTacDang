using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Organization;

/// <summary>Một tổ chức Đảng chưa xóa, đủ để đi ngược lên cha và kiểm tra loại.</summary>
public readonly record struct PartyUnitNode(Guid Id, Guid? ParentId, Guid? UnitTypeId, bool IsActive);

/// <summary>Luật tổ chức Đảng: nơi sinh hoạt của đảng viên và tổ chức giữ chức vụ Đảng có cấp.</summary>
public static class PartyUnitRules
{
    /// <summary>Phần sau "… không thành công: " khi chọn tổ chức có tổ chức con làm nơi sinh hoạt.</summary>
    public const string MustBeCellReason = "đảng viên phải sinh hoạt tại một chi bộ, không chọn Đảng bộ có chi bộ trực thuộc.";

    /// <summary>Có tổ chức con đang hoạt động thì không làm nơi sinh hoạt được.</summary>
    public static bool HasActiveChildren(IEnumerable<PartyUnitNode> nodes, Guid id)
        => nodes.Any(n => n.ParentId == id && n.IsActive);

    /// <summary>Tổ chức đang hoạt động gần nhất đúng loại, xét từ <paramref name="startId"/> rồi lên cha; null = không có.</summary>
    public static Guid? FindNearestOfType(IReadOnlyDictionary<Guid, PartyUnitNode> nodes, Guid? startId, Guid unitTypeId)
    {
        var visited = new HashSet<Guid>();
        var current = startId;
        while (current.HasValue && visited.Add(current.Value) && nodes.TryGetValue(current.Value, out var node))
        {
            if (node.IsActive && node.UnitTypeId == unitTypeId)
                return node.Id;
            current = node.ParentId;
        }
        return null;
    }
}
