using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports;

#region Tra cứu dữ liệu tham chiếu

/// <summary>Đơn vị chính quyền / tổ chức Đảng (kể cả đã xóa) để đối chiếu mã khi nhập.</summary>
public sealed record OrgUnitRef(Guid Id, string Code, string Name, Guid? ParentId, Guid? UnitTypeId, bool IsActive, bool IsDeleted);

public sealed record UnitTypeRef(Guid Id, string Name, OrgSide Side, bool IsActive);

public sealed record PositionRef(Guid Id, string Name, string? StatCode, PositionSide Side, bool IsActive);

public sealed record FormLayoutRef(Guid Id, string Name);

public sealed record RoleRef(Guid Id, string Code, string Name, IReadOnlyList<string> PermissionCodes);

/// <summary>Tài khoản (kể cả đã xóa) kèm chức vụ đang giữ.</summary>
public sealed record UserRef(
    Guid Id,
    string Username,
    string FullName,
    bool IsDeleted,
    Guid? DepartmentId,
    Guid? PartyCellId,
    IReadOnlyList<Guid> HeldPositionIds);

/// <summary>Bản gán vai trò đang/sắp hiệu lực.</summary>
public sealed record AssignmentRef(Guid UserId, Guid RoleId, RoleScopeType ScopeType, Guid? ScopeId, DateTime ValidFrom, DateTime? ValidTo);

/// <summary>Đọc dữ liệu tham chiếu theo lô cho bước kiểm tra của các loại nhập (không theo dõi thay đổi).</summary>
public interface IImportLookup
{
    /// <summary>Đơn vị chính quyền, kể cả đã xóa.</summary>
    Task<IReadOnlyList<OrgUnitRef>> GetDepartmentsAsync(CancellationToken ct = default);

    /// <summary>Tổ chức Đảng, kể cả đã xóa.</summary>
    Task<IReadOnlyList<OrgUnitRef>> GetPartyCellsAsync(CancellationToken ct = default);

    /// <summary>Loại đơn vị chưa xóa.</summary>
    Task<IReadOnlyList<UnitTypeRef>> GetUnitTypesAsync(CancellationToken ct = default);

    /// <summary>Chức vụ chưa xóa.</summary>
    Task<IReadOnlyList<PositionRef>> GetPositionsAsync(CancellationToken ct = default);

    /// <summary>Vai trò chưa xóa kèm mã quyền.</summary>
    Task<IReadOnlyList<RoleRef>> GetRolesAsync(CancellationToken ct = default);

    /// <summary>Kiểu trình bày biểu mẫu chưa xóa.</summary>
    Task<IReadOnlyList<FormLayoutRef>> GetFormLayoutsAsync(CancellationToken ct = default);

    /// <summary>Tài khoản theo tên đăng nhập (đã chuẩn hóa chữ thường), kể cả đã xóa; chức vụ đang giữ tại <paramref name="now"/>.</summary>
    Task<IReadOnlyList<UserRef>> FindUsersAsync(IReadOnlyCollection<string> usernames, DateTime now, CancellationToken ct = default);

    /// <summary>Bản gán chưa xóa, đang hoặc sắp hiệu lực tại <paramref name="now"/>, của các người cho trước.</summary>
    Task<IReadOnlyList<AssignmentRef>> GetCurrentOrFutureAssignmentsAsync(IReadOnlyCollection<Guid> userIds, DateTime now, CancellationToken ct = default);
}

/// <summary>Ghi một bản ghi nhật ký tóm tắt cho mỗi lần xác nhận nhập (ngoài nhật ký tự động của từng bản ghi).</summary>
public interface IImportAuditWriter
{
    /// <summary>Thêm bản ghi nhật ký (lưu cùng lần lưu kế tiếp của DbContext).</summary>
    void AddSummary(string kind, string fileName, int rows, int created, int updated);
}

#endregion

#region File Excel

public interface IImportWorkbook
{
    /// <summary>
    /// Dòng 1 là tiêu đề, bỏ dòng trống. File hỏng, thiếu cột bắt buộc, quá số dòng → <see cref="Common.Exceptions.ValidationException"/>.
    /// </summary>
    IReadOnlyList<ImportRawRow> Read(Stream content, IReadOnlyList<ImportColumn> columns);

    /// <summary>Dựng file mẫu: sheet dữ liệu (tiêu đề cố định, danh sách chọn) và sheet Hướng dẫn.</summary>
    byte[] BuildTemplate(string title, string description, IReadOnlyList<ImportColumn> columns);

    /// <summary>Dựng file bảng đơn giản, chống chèn công thức.</summary>
    byte[] BuildTable(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);
}

#endregion

#region Phiên nhập

/// <summary>Phiên xem trước đang chờ xác nhận.</summary>
/// <param name="OwnerId">Chỉ người tải lên được xác nhận.</param>
/// <param name="Rows">Phân tích lại khi xác nhận.</param>
/// <param name="ExpiresAt">UTC.</param>
public sealed record ImportSession(Guid Id, string Kind, Guid OwnerId, string FileName, IReadOnlyList<ImportRawRow> Rows, DateTime ExpiresAt);

/// <summary>Lưu tạm phiên xem trước và file kết quả trong bộ nhớ, không ghi ra đĩa.</summary>
public interface IImportSessionStore
{
    ImportSession Create(string kind, Guid ownerId, string fileName, IReadOnlyList<ImportRawRow> rows);

    /// <summary>Lấy phiên chưa hết hạn; null nếu không có.</summary>
    ImportSession? Get(Guid id);

    /// <summary>Đánh dấu phiên đang xác nhận; false nếu phiên đang được xác nhận ở request khác.</summary>
    bool TryBeginCommit(Guid id);

    /// <summary>Kết thúc xác nhận: thành công → xóa phiên; thất bại → cho xác nhận lại.</summary>
    void EndCommit(Guid id, bool succeeded);

    /// <summary>Lưu file kết quả tải một lần; trả mã tải và thời điểm hết hạn.</summary>
    (string Token, DateTime ExpiresAt) SaveResult(Guid ownerId, ImportFile file);

    /// <summary>Lấy và xóa file kết quả (chỉ người tạo); null nếu không có, hết hạn hoặc đã tải.</summary>
    ImportFile? TakeResult(string token, Guid ownerId);
}

#endregion
