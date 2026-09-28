using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện repository quản lý hồ sơ Cán bộ / Đảng viên
/// </summary>
public interface IUserRepository : IRepository<PartyMemberProfile>
{
    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập</summary>
    Task<PartyMemberProfile?> GetByUsernameAsync(string username);

    /// <summary>Lấy thông tin cán bộ đầu tiên trong hệ thống</summary>
    Task<PartyMemberProfile?> GetFirstMemberAsync();

    /// <summary>Lấy toàn bộ danh sách cán bộ kèm thông tin Chi bộ và Phòng ban</summary>
    Task<List<PartyMemberProfile>> GetAllWithDetailsAsync();

    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập kèm thông tin Vai trò và Quyền hạn</summary>
    Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username);

    /// <summary>Tìm kiếm cán bộ theo Id kèm thông tin Vai trò và Quyền hạn</summary>
    Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id);
}

/// <summary>
/// Giao diện repository quản trị Vai trò và Quyền hạn (Dynamic RBAC)
/// </summary>
public interface IRoleRepository
{
    /// <summary>Lấy danh sách tất cả các vai trò kèm quyền hạn</summary>
    Task<List<AppRole>> GetAllRolesWithPermissionsAsync();

    /// <summary>Lấy danh sách tất cả các quyền hạn trong hệ thống</summary>
    Task<List<Permission>> GetAllPermissionsAsync();

    /// <summary>Lấy thông tin vai trò theo Id kèm quyền hạn</summary>
    Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId);

    /// <summary>Lấy thông tin vai trò theo mã code</summary>
    Task<AppRole?> GetRoleByCodeAsync(string roleCode);

    /// <summary>Cập nhật danh sách quyền hạn cho một vai trò</summary>
    Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes);

    /// <summary>Gán danh sách vai trò cho một cán bộ / người dùng</summary>
    Task AssignRolesToUserAsync(Guid userId, IEnumerable<string> roleCodes);
}

/// <summary>
/// Giao diện repository quản lý tệp đính kèm và minh chứng
/// </summary>
public interface IAttachmentRepository : IRepository<TaskAttachment>
{
    /// <summary>Lấy toàn bộ danh sách tệp đính kèm (chỉ phiên bản hiện hành) theo thời gian mới nhất</summary>
    Task<List<TaskAttachment>> GetAllAttachmentsAsync();
}

/// <summary>
/// Truy vấn/ghi tệp theo đối tượng sở hữu và phiên bản (T-36).
/// Tách khỏi <see cref="IAttachmentRepository"/> để không đổi hợp đồng repository sẵn có.
/// </summary>
public interface IAttachmentVersionRepository
{
    /// <summary>Lấy các tệp (phiên bản hiện hành) của một đối tượng, gồm cả dữ liệu cũ gắn qua RecordId/TaskId/EvaluationTask.AttachmentId.</summary>
    Task<List<TaskAttachment>> GetCurrentByOwnerAsync(string ownerType, Guid ownerId);

    /// <summary>Lấy phiên bản hiện hành của nhóm chứa phiên bản <paramref name="anyVersionId"/> (null nếu không tồn tại hoặc đã xóa).</summary>
    Task<TaskAttachment?> GetCurrentVersionAsync(Guid anyVersionId);

    /// <summary>Lấy mọi phiên bản (chưa xóa) của một nhóm, sắp theo số phiên bản tăng dần.</summary>
    Task<List<TaskAttachment>> GetVersionsAsync(Guid groupId);

    /// <summary>Lưu phiên bản mới và đánh dấu phiên bản trước không còn hiện hành trong cùng một lần lưu.</summary>
    Task AddVersionAsync(TaskAttachment previous, TaskAttachment next);

    /// <summary>Xóa mềm mọi phiên bản của một nhóm.</summary>
    Task SoftDeleteGroupAsync(Guid groupId);
}

/// <summary>Tra cứu dữ liệu phục vụ kiểm tra quyền trên tệp đính kèm.</summary>
public interface IAttachmentAccessReader
{
    /// <summary>Trả về tập Id (trong <paramref name="userIds"/>) của người dùng đang có vai trò <paramref name="roleCode"/>.</summary>
    Task<HashSet<Guid>> GetUserIdsInRoleAsync(IReadOnlyCollection<Guid> userIds, string roleCode);

    /// <summary>
    /// Lấy các hồ sơ đánh giá (kèm Member) mà mỗi tệp đang gắn vào, qua TaskAttachment.RecordId
    /// hoặc qua EvaluationTask.AttachmentId. Khóa là Id tệp; tệp không gắn hồ sơ không có trong kết quả.
    /// </summary>
    Task<Dictionary<Guid, List<AttachmentRecordLink>>> GetRecordLinksAsync(IReadOnlyCollection<TaskAttachment> attachments);

    /// <summary>
    /// Lấy hồ sơ đánh giá (kèm Member) của đối tượng sở hữu tệp: chính hồ sơ với <see cref="AttachmentOwnerTypes.EvaluationRecord"/>,
    /// hồ sơ chứa nhiệm vụ với <see cref="AttachmentOwnerTypes.EvaluationTask"/>. Null nếu không tìm thấy.
    /// </summary>
    Task<EvaluationRecord?> GetOwnerRecordAsync(string ownerType, Guid ownerId);
}

/// <summary>Giao diện tra cứu audit log tập trung.</summary>
public interface IAuditRepository
{
    /// <summary>Lấy audit log mới nhất theo entity, có giới hạn số lượng bản ghi.</summary>
    Task<List<AuditLog>> GetAuditLogsAsync(string? entityType, string? entityId, int limit);
}

/// <summary>
/// Giao diện repository quản lý tổ chức Chi bộ và Phòng ban
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>Lấy danh sách Chi bộ kèm danh sách Đảng viên</summary>
    Task<List<PartyCell>> GetPartyCellsWithMembersAsync();

    /// <summary>Lấy danh sách Phòng ban chuyên môn kèm danh sách cán bộ</summary>
    Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync();

    /// <summary>Lấy thông tin Chi bộ theo Id</summary>
    Task<PartyCell?> GetPartyCellByIdAsync(Guid id);

    /// <summary>Thêm mới Chi bộ</summary>
    Task AddPartyCellAsync(PartyCell cell);

    /// <summary>Cập nhật thông tin Chi bộ</summary>
    Task UpdatePartyCellAsync(PartyCell cell);

    /// <summary>Xóa Chi bộ</summary>
    Task DeletePartyCellAsync(PartyCell cell);

    // ----- Danh mục (task 10): các thao tác ghi chỉ đưa vào DbContext, lưu qua IUnitOfWork -----

    /// <summary>Danh sách Phòng/đơn vị chưa xóa (không tải cán bộ), sắp theo thứ tự hiển thị rồi mã.</summary>
    Task<List<AdministrativeDepartment>> ListDepartmentsAsync();

    /// <summary>Danh sách Chi bộ chưa xóa (không tải cán bộ), sắp theo thứ tự hiển thị rồi mã.</summary>
    Task<List<PartyCell>> ListPartyCellsAsync();

    /// <summary>Số cán bộ (chưa xóa) theo từng Phòng.</summary>
    Task<Dictionary<Guid, int>> CountMembersByDepartmentAsync();

    /// <summary>Số cán bộ (chưa xóa) theo từng Chi bộ.</summary>
    Task<Dictionary<Guid, int>> CountMembersByPartyCellAsync();

    /// <summary>Phòng chưa xóa theo Id (được theo dõi để cập nhật).</summary>
    Task<AdministrativeDepartment?> FindDepartmentAsync(Guid id);

    /// <summary>Chi bộ chưa xóa theo Id (được theo dõi để cập nhật).</summary>
    Task<PartyCell?> FindPartyCellAsync(Guid id);

    /// <summary>Toàn bộ Phòng kể cả đã xóa mềm (được theo dõi) — dùng kiểm tra mã và import.</summary>
    Task<List<AdministrativeDepartment>> ListDepartmentsIncludingDeletedAsync();

    /// <summary>Toàn bộ Chi bộ kể cả đã xóa mềm (được theo dõi) — dùng kiểm tra mã và import.</summary>
    Task<List<PartyCell>> ListPartyCellsIncludingDeletedAsync();

    /// <summary>Mã Phòng đã được dùng (kể cả bản ghi đã xóa mềm, không phân biệt hoa thường).</summary>
    Task<bool> DepartmentCodeExistsAsync(string code, Guid? excludeId = null);

    /// <summary>Mã Chi bộ đã được dùng (kể cả bản ghi đã xóa mềm, không phân biệt hoa thường).</summary>
    Task<bool> PartyCellCodeExistsAsync(string code, Guid? excludeId = null);

    /// <summary>Đưa Phòng mới vào DbContext (chưa lưu).</summary>
    void AddDepartment(AdministrativeDepartment department);

    /// <summary>Đưa Chi bộ mới vào DbContext (chưa lưu).</summary>
    void AddPartyCell(PartyCell cell);

    /// <summary>Đánh dấu xóa Phòng (DbContext đổi thành xóa mềm khi lưu).</summary>
    void RemoveDepartment(AdministrativeDepartment department);

    /// <summary>Đánh dấu xóa Chi bộ (DbContext đổi thành xóa mềm khi lưu).</summary>
    void RemovePartyCell(PartyCell cell);

    /// <summary>Số dữ liệu còn tham chiếu tới Phòng (cán bộ, hồ sơ đánh giá cá nhân, hồ sơ tập thể).</summary>
    Task<CatalogUsage> GetDepartmentUsageAsync(Guid id);

    /// <summary>Số dữ liệu còn tham chiếu tới Chi bộ (cán bộ, hồ sơ đánh giá cá nhân, hồ sơ tập thể, biên bản hội nghị).</summary>
    Task<CatalogUsage> GetPartyCellUsageAsync(Guid id);
}

/// <summary>Số dữ liệu còn tham chiếu tới một mục danh mục (chặn xóa khi khác 0).</summary>
/// <param name="Members">Cán bộ chưa xóa.</param>
/// <param name="EvaluationRecords">Hồ sơ đánh giá cá nhân chưa xóa.</param>
/// <param name="CollectiveRecords">Hồ sơ tự đánh giá tập thể chưa xóa.</param>
/// <param name="Meetings">Biên bản hội nghị, kiểm phiếu chưa xóa (chỉ Chi bộ).</param>
public sealed record CatalogUsage(int Members, int EvaluationRecords, int CollectiveRecords, int Meetings)
{
    /// <summary>Tổng hồ sơ đánh giá (cá nhân + tập thể + biên bản).</summary>
    public int Records => EvaluationRecords + CollectiveRecords + Meetings;

    /// <summary>Không còn dữ liệu nào tham chiếu.</summary>
    public bool IsUnused => Members == 0 && Records == 0;
}

/// <summary>
/// Giao diện repository quản lý kỳ đánh giá và bảng đánh giá cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public interface IEvaluationRepository
{
    /// <summary>Lấy danh sách tất cả các kỳ đánh giá (sắp xếp giảm dần theo năm và quý)</summary>
    Task<List<EvaluationPeriod>> GetPeriodsAsync();

    /// <summary>Lấy thông tin kỳ đánh giá theo Id</summary>
    Task<EvaluationPeriod?> GetPeriodByIdAsync(Guid id);

    /// <summary>Lấy kỳ đánh giá đang hoạt động</summary>
    Task<EvaluationPeriod?> GetActivePeriodAsync();

    /// <summary>Thêm mới kỳ đánh giá</summary>
    Task AddPeriodAsync(EvaluationPeriod period);

    /// <summary>Cập nhật thông tin và trạng thái kỳ đánh giá</summary>
    Task UpdatePeriodAsync(EvaluationPeriod period);

    /// <summary>Lấy hồ sơ đánh giá của một cán bộ trong kỳ cụ thể kèm danh sách công việc</summary>
    Task<EvaluationRecord?> GetRecordAsync(Guid periodId, Guid memberId);

    /// <summary>Lấy hồ sơ đánh giá theo Id kèm thông tin Cán bộ và danh sách công việc</summary>
    Task<EvaluationRecord?> GetRecordByIdAsync(Guid id);

    /// <summary>Lấy lịch sử chuyển trạng thái của một hồ sơ đánh giá.</summary>
    Task<List<EvaluationRecordHistory>> GetRecordHistoriesAsync(Guid recordId);

    /// <summary>Lấy toàn bộ danh sách hồ sơ đánh giá của một kỳ kèm thông tin Cán bộ, Chi bộ, Phòng ban</summary>
    Task<List<EvaluationRecord>> GetRecordsByPeriodAsync(Guid periodId);

    /// <summary>Lấy danh sách hồ sơ đánh giá của một Chi bộ trong kỳ cụ thể</summary>
    Task<List<EvaluationRecord>> GetRecordsByBranchAsync(Guid periodId, Guid branchId);

    /// <summary>Thêm mới hồ sơ đánh giá</summary>
    Task AddRecordAsync(EvaluationRecord record);

    /// <summary>Cập nhật hồ sơ đánh giá</summary>
    Task UpdateRecordAsync(EvaluationRecord record);

    /// <summary>Ghi nhận lịch sử chuyển trạng thái hồ sơ đánh giá</summary>
    Task AddRecordHistoryAsync(EvaluationRecordHistory history);

    /// <summary>Lấy danh sách các công việc đăng ký theo Id hồ sơ đánh giá</summary>
    Task<List<EvaluationTask>> GetTasksByRecordIdAsync(Guid recordId);

    /// <summary>Thay thế toàn bộ danh sách công việc đăng ký của một hồ sơ đánh giá</summary>
    Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks);
}

/// <summary>Repository hồ sơ đánh giá tập thể Mẫu 06, 07, 08.</summary>
public interface ICollectiveEvaluationRepository
{
    /// <summary>Lấy hồ sơ tập thể theo mã hồ sơ.</summary>
    Task<CollectiveEvaluationRecord?> GetByIdAsync(Guid id);

    /// <summary>Lấy các hồ sơ tập thể trong một kỳ đánh giá.</summary>
    Task<List<CollectiveEvaluationRecord>> GetByPeriodAsync(Guid periodId, CollectiveEvaluationForm? form = null);

    /// <summary>Thêm hồ sơ tập thể.</summary>
    Task AddAsync(CollectiveEvaluationRecord record);

    /// <summary>Cập nhật hồ sơ tập thể.</summary>
    Task UpdateAsync(CollectiveEvaluationRecord record);
}

/// <summary>Repository biên bản hội nghị và kết quả kiểm phiếu Mẫu 12, 13.</summary>
public interface IEvaluationMeetingRepository
{
    /// <summary>Lấy biên bản theo mã.</summary>
    Task<EvaluationMeeting?> GetByIdAsync(Guid id);

    /// <summary>Lấy các biên bản trong một kỳ đánh giá.</summary>
    Task<List<EvaluationMeeting>> GetByPeriodAsync(Guid periodId, Guid? partyCellId = null);

    /// <summary>Thêm biên bản hội nghị.</summary>
    Task AddAsync(EvaluationMeeting meeting);

    /// <summary>Cập nhật biên bản hội nghị.</summary>
    Task UpdateAsync(EvaluationMeeting meeting);
}
