using System;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Tra cứu quan hệ cây đơn vị (đơn vị chính quyền, tổ chức Đảng) theo đường dẫn vật hóa <c>Path</c>.</summary>
public interface IOrgSubtreeReader
{
    /// <summary>Gồm cả chính <paramref name="rootId"/>.</summary>
    Task<bool> IsDepartmentWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default);

    /// <summary>Gồm cả chính <paramref name="rootId"/>.</summary>
    Task<bool> IsPartyCellWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default);
}

/// <summary>Luật "hồ sơ thuộc tổ chức của biên bản" dùng chung khi gắn kết quả kiểm phiếu vào biên bản.</summary>
public static class OrgSubtreeRules
{
    /// <summary>Hồ sơ phải ở đơn vị/tổ chức của biên bản hoặc cây con; biên bản không gắn đơn vị (cấp Công ty) nhận mọi hồ sơ.</summary>
    public static async Task<bool> RecordWithinMeetingAsync(
        IOrgSubtreeReader reader,
        Guid? meetingDepartmentId,
        Guid? meetingPartyCellId,
        Guid? recordDepartmentId,
        Guid? recordPartyCellId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (meetingPartyCellId.HasValue
            && (!recordPartyCellId.HasValue
                || (recordPartyCellId != meetingPartyCellId
                    && !await reader.IsPartyCellWithinAsync(meetingPartyCellId.Value, recordPartyCellId.Value, ct))))
            return false;
        if (meetingDepartmentId.HasValue
            && (!recordDepartmentId.HasValue
                || (recordDepartmentId != meetingDepartmentId
                    && !await reader.IsDepartmentWithinAsync(meetingDepartmentId.Value, recordDepartmentId.Value, ct))))
            return false;
        return true;
    }
}
