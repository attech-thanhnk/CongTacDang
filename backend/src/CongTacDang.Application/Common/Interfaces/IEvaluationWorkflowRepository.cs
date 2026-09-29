using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Cán bộ có thể thêm vào danh sách được đánh giá (dữ liệu chỉ đọc).</summary>
/// <param name="Id">Id cán bộ.</param>
/// <param name="Username">Tên đăng nhập.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="DepartmentId">Phòng hiện tại.</param>
/// <param name="DepartmentName">Tên Phòng.</param>
/// <param name="PartyCellId">Chi bộ hiện tại.</param>
/// <param name="PartyCellName">Tên Chi bộ.</param>
/// <param name="WeightFrameCode">Mã khung tỷ trọng mặc định của cán bộ.</param>
/// <param name="ApprovalAuthority">Cấp quyết định.</param>
/// <param name="IsActive">Tài khoản đang hoạt động.</param>
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

/// <summary>
/// Truy cập dữ liệu cho luồng đánh giá theo cấu hình kỳ (task 12). Các thao tác ghi chỉ đưa vào DbContext;
/// lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IEvaluationWorkflowRepository
{
    /// <summary>Kỳ theo Id (được theo dõi để cập nhật).</summary>
    Task<EvaluationPeriod?> FindPeriodAsync(Guid id, CancellationToken ct = default);

    /// <summary>Các kỳ chưa xóa, mới nhất trước (không theo dõi).</summary>
    Task<List<EvaluationPeriod>> ListPeriodsAsync(CancellationToken ct = default);

    /// <summary>Số hồ sơ (người được đánh giá) theo kỳ.</summary>
    Task<Dictionary<Guid, int>> CountRecordsByPeriodAsync(CancellationToken ct = default);

    /// <summary>Đưa kỳ mới vào DbContext.</summary>
    void AddPeriod(EvaluationPeriod period);

    /// <summary>Hồ sơ theo Id kèm kỳ, cán bộ, Phòng/Chi bộ ảnh chụp, nhiệm vụ (được theo dõi).</summary>
    Task<EvaluationRecord?> FindRecordAsync(Guid id, CancellationToken ct = default);

    /// <summary>Hồ sơ của một cán bộ trong kỳ, kể cả đã xóa mềm (được theo dõi) — để thêm lại người đã bị bỏ.</summary>
    Task<EvaluationRecord?> FindRecordIncludingDeletedAsync(Guid periodId, Guid memberId, CancellationToken ct = default);

    /// <summary>Hồ sơ của kỳ kèm cán bộ, Phòng/Chi bộ ảnh chụp (không theo dõi).</summary>
    Task<List<EvaluationRecord>> ListRecordsAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Hồ sơ của kỳ (được theo dõi) — để tính lại trạng thái đầu khi cấu hình đổi.</summary>
    Task<List<EvaluationRecord>> ListRecordsForUpdateAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Hồ sơ đang ở các trạng thái cho trước, trong các kỳ cho trước (không theo dõi).</summary>
    Task<List<EvaluationRecord>> ListRecordsByStatusAsync(IReadOnlyCollection<Guid> periodIds, IReadOnlyCollection<RecordStatus> statuses, CancellationToken ct = default);

    /// <summary>Id cán bộ đã có hồ sơ (chưa xóa) trong kỳ.</summary>
    Task<HashSet<Guid>> GetParticipantIdsAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Đưa hồ sơ mới vào DbContext.</summary>
    void AddRecord(EvaluationRecord record);

    /// <summary>Đánh dấu xóa hồ sơ (DbContext đổi thành xóa mềm khi lưu).</summary>
    void RemoveRecord(EvaluationRecord record);

    /// <summary>Thay toàn bộ nhiệm vụ của hồ sơ (nhiệm vụ cũ bị xóa mềm).</summary>
    Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks, CancellationToken ct = default);

    /// <summary>Nhiệm vụ của hồ sơ (được theo dõi).</summary>
    Task<List<EvaluationTask>> GetTasksAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Đưa lịch sử vào DbContext.</summary>
    void AddHistory(EvaluationRecordHistory history);

    /// <summary>Lịch sử của hồ sơ theo thời gian tăng dần.</summary>
    Task<List<EvaluationRecordHistory>> ListHistoryAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Đưa kết quả ghi nhận của bước do cấp trên thực hiện vào DbContext.</summary>
    void AddExternalResult(EvaluationExternalResult result);

    /// <summary>
    /// Id các tài khoản đang hoạt động (chưa xóa) có bản gán đang hiệu lực tại <paramref name="now"/> của vai trò chứa ít nhất một
    /// mã quyền trong <paramref name="permissionCodes"/> — ứng viên để kiểm tra kẹt luồng (quyền chính xác tính qua resolver/guard).
    /// </summary>
    Task<List<Guid>> ListActiveUserIdsWithAnyPermissionAsync(IReadOnlyCollection<string> permissionCodes, DateTime now, CancellationToken ct = default);

    /// <summary>Đưa dòng kết quả kiểm phiếu mới vào DbContext.</summary>
    void AddVoteSummary(EvaluationMeetingVoteSummary summary);

    /// <summary>Biên bản theo Id kèm tổng hợp phiếu (được theo dõi).</summary>
    Task<EvaluationMeeting?> FindMeetingAsync(Guid id, CancellationToken ct = default);

    /// <summary>Thông tin cán bộ để chụp ảnh khi thêm vào kỳ (chưa xóa).</summary>
    Task<List<MemberSnapshotSource>> GetMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default);

    /// <summary>Cán bộ đang hoạt động theo Phòng/Chi bộ (null = mọi cán bộ), có lọc theo tên/tài khoản.</summary>
    Task<List<MemberSnapshotSource>> SearchMembersAsync(Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default);

    /// <summary>Cán bộ (chưa xóa) theo tên đăng nhập, không phân biệt hoa thường — dùng cho import.</summary>
    Task<List<MemberSnapshotSource>> FindMembersByUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct = default);

    /// <summary>Tên đầy đủ của cán bộ (để ghi người thực hiện).</summary>
    Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Phòng tồn tại (chưa xóa).</summary>
    Task<bool> DepartmentExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chi bộ tồn tại (chưa xóa).</summary>
    Task<bool> PartyCellExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// T-53: tên tệp minh chứng theo <b>phiên bản hiện hành</b> của nhóm tệp mà từng nhiệm vụ đang trỏ tới
    /// (khóa = Id nhiệm vụ). Nhiệm vụ không có tệp hoặc tệp đã xóa không có trong kết quả.
    /// </summary>
    Task<Dictionary<Guid, (Guid AttachmentId, string FileName)>> GetCurrentEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default);
}
