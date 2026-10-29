using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

public interface IUserRepository : IRepository<PartyMemberProfile>
{
    Task<PartyMemberProfile?> GetByUsernameAsync(string username);

    Task<PartyMemberProfile?> GetFirstMemberAsync();

    /// <summary>Kèm Chi bộ và Phòng.</summary>
    Task<List<PartyMemberProfile>> GetAllWithDetailsAsync();

    /// <summary>Kèm Chi bộ, Phòng; không theo dõi. Vai trò/quyền lấy từ <c>IPermissionResolver</c>.</summary>
    Task<PartyMemberProfile?> GetWithOrganizationByIdAsync(Guid id);
}

public interface IAttachmentRepository : IRepository<TaskAttachment>
{
    /// <summary>Chỉ phiên bản hiện hành, mới nhất trước.</summary>
    Task<List<TaskAttachment>> GetCurrentPageAsync(int skip, int take);
}

/// <summary>Truy vấn/ghi tệp theo đối tượng sở hữu và phiên bản.</summary>
public interface IAttachmentVersionRepository
{
    /// <summary>
    /// Phiên bản hiện hành của tệp gắn qua RecordId/TaskId/EvaluationTask.AttachmentId;
    /// với hồ sơ đánh giá gồm cả văn bản của cấp trên.
    /// </summary>
    Task<List<TaskAttachment>> GetCurrentByOwnerAsync(string ownerType, Guid ownerId);

    /// <summary>Phiên bản hiện hành của nhóm chứa <paramref name="anyVersionId"/>; null nếu không có hoặc đã xóa.</summary>
    Task<TaskAttachment?> GetCurrentVersionAsync(Guid anyVersionId);

    /// <summary>Các phiên bản chưa xóa, số phiên bản tăng dần.</summary>
    Task<List<TaskAttachment>> GetVersionsAsync(Guid groupId);

    /// <summary>Lưu phiên bản mới và bỏ cờ hiện hành của bản trước trong cùng một lần lưu.</summary>
    Task AddVersionAsync(TaskAttachment previous, TaskAttachment next);

    /// <summary>Xóa mềm mọi phiên bản của nhóm; tệp trên đĩa giữ nguyên.</summary>
    Task RemoveGroupAsync(Guid groupId);
}

/// <summary>Tra cứu dữ liệu phục vụ kiểm tra quyền trên tệp đính kèm.</summary>
public interface IAttachmentAccessReader
{
    /// <summary>
    /// Hồ sơ (kèm Member) mà mỗi tệp đang gắn vào, theo Id tệp;
    /// tệp không gắn hồ sơ không có trong kết quả.
    /// </summary>
    Task<Dictionary<Guid, List<AttachmentRecordLink>>> GetRecordLinksAsync(IReadOnlyCollection<TaskAttachment> attachments);

    /// <summary>Hồ sơ đánh giá (kèm Member) của đối tượng sở hữu: hồ sơ, nhiệm vụ hoặc kiến nghị; null nếu không thấy.</summary>
    Task<EvaluationRecord?> GetOwnerRecordAsync(string ownerType, Guid ownerId);

    /// <summary>Kiến nghị còn đang xử lý (đã gửi / đang xem xét); null nếu không tìm thấy.</summary>
    Task<bool?> IsAppealOpenAsync(Guid appealId);

    /// <summary>Tổ chức và trạng thái kỳ của hồ sơ tập thể, theo Id hồ sơ; hồ sơ không tồn tại/đã xóa bị bỏ qua.</summary>
    Task<Dictionary<Guid, CollectiveAttachmentOwner>> GetCollectiveOwnersAsync(IReadOnlyCollection<Guid> collectiveRecordIds);

    /// <summary>Phiếu đối soát / phiếu kiểm soát / biên bản hiệu chuẩn (<paramref name="ownerType"/>) theo Id; phiếu không tồn tại hoặc đã xóa bị bỏ qua.</summary>
    Task<Dictionary<Guid, FormAttachmentOwner>> GetFormOwnersAsync(string ownerType, IReadOnlyCollection<Guid> ownerIds);
}

public interface IAuditRepository
{
    /// <summary>Mới nhất trước; số đếm tính trên toàn bộ kết quả lọc.</summary>
    Task<AuditLogSearchResult> SearchAuditLogsAsync(AuditLogCriteria criteria, CancellationToken ct = default);
}

/// <summary>Trang đã chuẩn hóa.</summary>
public sealed record AuditLogCriteria(int Page, int PageSize, string? EntityType = null, string? EntityId = null, string? Action = null);

public sealed class AuditLogSearchResult
{
    public IReadOnlyList<AuditLog> Items { get; init; } = Array.Empty<AuditLog>();

    /// <summary>Tổng số bản ghi khớp bộ lọc.</summary>
    public int TotalCount { get; init; }

    public IReadOnlyDictionary<string, int> ActionCounts { get; init; } = new Dictionary<string, int>();

    /// <summary>Số người thao tác khác nhau.</summary>
    public int ActorCount { get; init; }

    /// <summary>Mọi loại đối tượng có trong nhật ký (không phụ thuộc bộ lọc).</summary>
    public IReadOnlyList<string> EntityTypes { get; init; } = Array.Empty<string>();
}

public interface IOrganizationRepository
{
    Task<List<PartyCell>> GetPartyCellsWithMembersAsync();

    Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync();

    Task<PartyCell?> GetPartyCellByIdAsync(Guid id);

    Task AddPartyCellAsync(PartyCell cell);

    Task UpdatePartyCellAsync(PartyCell cell);

    Task DeletePartyCellAsync(PartyCell cell);

    // Các thao tác ghi dưới đây chỉ đưa vào DbContext, lưu qua IUnitOfWork.

    /// <summary>Chưa xóa, không tải cán bộ; sắp theo thứ tự hiển thị rồi mã.</summary>
    Task<List<AdministrativeDepartment>> ListDepartmentsAsync();

    /// <summary>Chưa xóa, không tải cán bộ; sắp theo thứ tự hiển thị rồi mã.</summary>
    Task<List<PartyCell>> ListPartyCellsAsync();

    Task<Dictionary<Guid, int>> CountMembersByDepartmentAsync();

    Task<Dictionary<Guid, int>> CountMembersByPartyCellAsync();

    /// <summary>Chưa xóa, được theo dõi.</summary>
    Task<AdministrativeDepartment?> FindDepartmentAsync(Guid id);

    /// <summary>Chưa xóa, được theo dõi.</summary>
    Task<PartyCell?> FindPartyCellAsync(Guid id);

    /// <summary>Kể cả đã xóa mềm (được theo dõi) — dùng kiểm tra mã và cây đơn vị.</summary>
    Task<List<AdministrativeDepartment>> ListDepartmentsIncludingDeletedAsync();

    /// <summary>Kể cả đã xóa mềm (được theo dõi) — dùng kiểm tra mã và cây đơn vị.</summary>
    Task<List<PartyCell>> ListPartyCellsIncludingDeletedAsync();

    /// <summary>Tính cả bản ghi đã xóa mềm, không phân biệt hoa thường.</summary>
    Task<bool> DepartmentCodeExistsAsync(string code, Guid? excludeId = null);

    /// <summary>Tính cả bản ghi đã xóa mềm, không phân biệt hoa thường.</summary>
    Task<bool> PartyCellCodeExistsAsync(string code, Guid? excludeId = null);

    void AddDepartment(AdministrativeDepartment department);

    void AddPartyCell(PartyCell cell);

    /// <summary>DbContext đổi thành xóa mềm khi lưu.</summary>
    void RemoveDepartment(AdministrativeDepartment department);

    /// <summary>DbContext đổi thành xóa mềm khi lưu.</summary>
    void RemovePartyCell(PartyCell cell);

    /// <summary>Bản gán vai trò và chức vụ chỉ tính khi chưa hết hạn tại <paramref name="now"/>.</summary>
    Task<CatalogUsage> GetDepartmentUsageAsync(Guid id, DateTime now);

    /// <summary>Bản gán vai trò và chức vụ chỉ tính khi chưa hết hạn tại <paramref name="now"/>.</summary>
    Task<CatalogUsage> GetPartyCellUsageAsync(Guid id, DateTime now);

    /// <summary>Số đơn vị con trực tiếp chưa xóa.</summary>
    Task<Dictionary<Guid, int>> CountChildDepartmentsAsync();

    /// <summary>Số tổ chức con trực tiếp chưa xóa.</summary>
    Task<Dictionary<Guid, int>> CountChildPartyCellsAsync();

    /// <summary>Chưa xóa, không theo dõi; sắp theo bên, thứ tự, tên.</summary>
    Task<List<OrgUnitType>> ListUnitTypesAsync();

    /// <summary>Chưa xóa, được theo dõi.</summary>
    Task<OrgUnitType?> FindUnitTypeAsync(Guid id);

    /// <summary>Trùng tên trong cùng bên, không phân biệt hoa thường.</summary>
    Task<bool> UnitTypeNameExistsAsync(OrgSide side, string name, Guid? excludeId = null);

    /// <summary>Số đơn vị chưa xóa (cả hai bên) theo từng loại.</summary>
    Task<Dictionary<Guid, int>> CountUnitsByTypeAsync();

    void AddUnitType(OrgUnitType type);

    /// <summary>Xóa mềm khi lưu.</summary>
    void RemoveUnitType(OrgUnitType type);
}

/// <summary>Số dữ liệu còn tham chiếu tới một đơn vị (chặn xóa khi khác 0); chỉ đếm bản ghi chưa xóa/chưa hết hạn.</summary>
/// <param name="Meetings">Chỉ có ở tổ chức Đảng.</param>
public sealed record CatalogUsage(int Members, int EvaluationRecords, int CollectiveRecords, int Meetings,
    int Children = 0, int Assignments = 0, int Positions = 0)
{
    public int Records => EvaluationRecords + CollectiveRecords + Meetings;

    public bool IsUnused => Members == 0 && Records == 0 && Children == 0 && Assignments == 0 && Positions == 0;
}

public interface IEvaluationRepository : IOrgSubtreeReader
{
    /// <summary>Giảm dần theo năm, quý.</summary>
    Task<List<EvaluationPeriod>> GetPeriodsAsync();

    Task<EvaluationPeriod?> GetPeriodByIdAsync(Guid id);

    Task<EvaluationPeriod?> GetActivePeriodAsync();

    Task AddPeriodAsync(EvaluationPeriod period);

    Task UpdatePeriodAsync(EvaluationPeriod period);

    /// <summary>Kèm danh sách công việc.</summary>
    Task<EvaluationRecord?> GetRecordAsync(Guid periodId, Guid memberId);

    /// <summary>Kèm cán bộ và danh sách công việc.</summary>
    Task<EvaluationRecord?> GetRecordByIdAsync(Guid id);

    Task<List<EvaluationRecordHistory>> GetRecordHistoriesAsync(Guid recordId);

    /// <summary>Kèm cán bộ, Chi bộ, Phòng.</summary>
    Task<List<EvaluationRecord>> GetRecordsByPeriodAsync(Guid periodId);

    Task<List<EvaluationRecord>> GetRecordsByBranchAsync(Guid periodId, Guid branchId);

    Task AddRecordAsync(EvaluationRecord record);

    Task UpdateRecordAsync(EvaluationRecord record);

    Task AddRecordHistoryAsync(EvaluationRecordHistory history);

    Task<List<EvaluationTask>> GetTasksByRecordIdAsync(Guid recordId);

    /// <summary>Thay toàn bộ danh sách công việc của hồ sơ.</summary>
    Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks);
}

/// <summary>Hồ sơ đánh giá tập thể (Mẫu 06, 07, 08).</summary>
public sealed record PartyCellMemberCount(Guid PartyCellId, string Name, int MemberCount);

public interface ICollectiveEvaluationRepository
{
    Task<CollectiveEvaluationRecord?> GetByIdAsync(Guid id);

    Task<List<CollectiveEvaluationRecord>> GetByPeriodAsync(Guid periodId, CollectiveEvaluationForm? form = null);

    Task AddAsync(CollectiveEvaluationRecord record);

    Task UpdateAsync(CollectiveEvaluationRecord record);

    /// <summary>Được theo dõi, kèm các dòng nội dung; lưu bằng <c>IUnitOfWork</c>.</summary>
    Task<CollectiveEvaluationRecord?> GetForUpdateAsync(Guid id);

    /// <summary>Tổ chức Đảng có hồ sơ cán bộ trong kỳ, kèm số hồ sơ.</summary>
    Task<List<PartyCellMemberCount>> GetPartyCellsWithRecordsAsync(Guid periodId);

    /// <summary>Xóa dòng cũ, thêm dòng mới trên hồ sơ đang được theo dõi; lưu bằng <c>IUnitOfWork</c>.</summary>
    void ReplaceItems(CollectiveEvaluationRecord record, IEnumerable<CollectiveEvaluationItem> items);
}

/// <summary>Biên bản hội nghị và kết quả kiểm phiếu (Mẫu 12, 13).</summary>
public interface IEvaluationMeetingRepository
{
    Task<EvaluationMeeting?> GetByIdAsync(Guid id);

    Task<List<EvaluationMeeting>> GetByPeriodAsync(Guid periodId, Guid? partyCellId = null);

    Task AddAsync(EvaluationMeeting meeting);

    Task UpdateAsync(EvaluationMeeting meeting);

    /// <summary>Được theo dõi; lưu bằng <c>IUnitOfWork</c>.</summary>
    Task<EvaluationMeeting?> GetForUpdateAsync(Guid id);
}
