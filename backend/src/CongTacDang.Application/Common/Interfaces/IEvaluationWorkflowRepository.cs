using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Cán bộ có thể thêm vào danh sách được đánh giá (chỉ đọc).</summary>
/// <param name="WeightFrameCode">Khung tỷ trọng mặc định của cán bộ.</param>
public sealed record MemberSnapshotSource(
    Guid Id,
    string Username,
    string FullName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PartyCellId,
    string? PartyCellName,
    string? WeightFrameCode,
    ApprovalAuthority ApprovalAuthority,
    bool IsActive);

/// <summary>Các thao tác ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface IEvaluationWorkflowRepository : IOrgSubtreeReader
{
    /// <summary>Được theo dõi.</summary>
    Task<EvaluationPeriod?> FindPeriodAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chưa xóa, mới nhất trước; không theo dõi.</summary>
    Task<List<EvaluationPeriod>> ListPeriodsAsync(CancellationToken ct = default);

    Task<Dictionary<Guid, int>> CountRecordsByPeriodAsync(CancellationToken ct = default);

    void AddPeriod(EvaluationPeriod period);

    /// <summary>Kèm kỳ, cán bộ, Phòng/Chi bộ ảnh chụp, nhiệm vụ; được theo dõi.</summary>
    Task<EvaluationRecord?> FindRecordAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kể cả đã xóa mềm (được theo dõi) — để thêm lại người đã bị bỏ.</summary>
    Task<EvaluationRecord?> FindRecordIncludingDeletedAsync(Guid periodId, Guid memberId, CancellationToken ct = default);

    /// <summary>Kèm cán bộ, Phòng/Chi bộ ảnh chụp; không theo dõi.</summary>
    Task<List<EvaluationRecord>> ListRecordsAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Được theo dõi — để tính lại trạng thái đầu khi cấu hình đổi.</summary>
    Task<List<EvaluationRecord>> ListRecordsForUpdateAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Không theo dõi.</summary>
    Task<List<EvaluationRecord>> ListRecordsByStatusAsync(IReadOnlyCollection<Guid> periodIds, IReadOnlyCollection<RecordStatus> statuses, CancellationToken ct = default);

    /// <summary>Id cán bộ đã có hồ sơ chưa xóa trong kỳ.</summary>
    Task<HashSet<Guid>> GetParticipantIdsAsync(Guid periodId, CancellationToken ct = default);

    void AddRecord(EvaluationRecord record);

    /// <summary>DbContext đổi thành xóa mềm khi lưu.</summary>
    void RemoveRecord(EvaluationRecord record);

    /// <summary>Nhiệm vụ cũ bị xóa mềm.</summary>
    Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks, CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<List<EvaluationTask>> GetTasksAsync(Guid recordId, CancellationToken ct = default);

    void AddHistory(EvaluationRecordHistory history);

    /// <summary>Theo thời gian tăng dần.</summary>
    Task<List<EvaluationRecordHistory>> ListHistoryAsync(Guid recordId, CancellationToken ct = default);

    void AddExternalResult(EvaluationExternalResult result);

    /// <summary>
    /// Ứng viên kiểm tra kẹt luồng: tài khoản đang hoạt động có bản gán hiệu lực chứa một trong các mã quyền.
    /// Phạm vi chưa xét — quyền chính xác tính qua resolver/guard.
    /// </summary>
    Task<List<Guid>> ListActiveUserIdsWithAnyPermissionAsync(IReadOnlyCollection<string> permissionCodes, DateTime now, CancellationToken ct = default);

    void AddVoteSummary(EvaluationMeetingVoteSummary summary);

    /// <summary>Kèm tổng hợp phiếu; được theo dõi.</summary>
    Task<EvaluationMeeting?> FindMeetingAsync(Guid id, CancellationToken ct = default);

    /// <summary>Thông tin cán bộ chưa xóa để chụp ảnh khi thêm vào kỳ.</summary>
    Task<List<MemberSnapshotSource>> GetMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default);

    /// <summary>Cán bộ đang hoạt động; Phòng/Chi bộ null = không lọc.</summary>
    Task<List<MemberSnapshotSource>> SearchMembersAsync(Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default);

    /// <summary>Tổ chức Đảng có tổ chức con — Đảng viên thường sinh hoạt ở tổ chức không có con.</summary>
    Task<HashSet<Guid>> GetPartyCellIdsWithChildrenAsync(CancellationToken ct = default);

    /// <summary>Tên vai trò chứa từng mã quyền (gợi ý người quản trị cần gán vai trò nào).</summary>
    Task<Dictionary<string, List<string>>> GetRoleNamesByPermissionAsync(IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);

    /// <summary>Hồ sơ của kỳ có lịch sử với hành động cho trước (ví dụ đã sửa tay ảnh chụp).</summary>
    Task<HashSet<Guid>> GetRecordIdsWithActionAsync(Guid periodId, WorkflowAction action, CancellationToken ct = default);

    Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default);

    Task<bool> DepartmentExistsAsync(Guid id, CancellationToken ct = default);

    Task<bool> PartyCellExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tệp minh chứng theo phiên bản hiện hành của nhóm, theo Id nhiệm vụ; nhiệm vụ không có tệp bị bỏ qua.</summary>
    Task<Dictionary<Guid, (Guid AttachmentId, string FileName)>> GetCurrentEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default);

    /// <summary>Nhiệm vụ có tệp minh chứng còn hiệu lực: tệp qua AttachmentId (bản hiện hành) hoặc tệp đính kèm thuộc nhiệm vụ.</summary>
    Task<HashSet<Guid>> GetTaskIdsWithEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default);
}
