using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Security;

/// <summary>Thao tác cần kiểm tra quyền trên một đối tượng cụ thể.</summary>
public enum AccessOperation
{
    /// <summary>Xem đối tượng.</summary>
    Read,

    /// <summary>Cập nhật nội dung đối tượng.</summary>
    Update,

    /// <summary>Xóa đối tượng.</summary>
    Delete,

    /// <summary>Kết xuất đối tượng ra file (Word/Excel).</summary>
    Export,

    /// <summary>Chi bộ nhận xét, bỏ phiếu hoặc lập biên bản cho hồ sơ/Chi bộ.</summary>
    BranchReview,

    /// <summary>Phê duyệt xếp loại chính thức.</summary>
    Approve
}

/// <summary>
/// Cơ chế kiểm tra quyền theo đối tượng dùng chung cho các Application service.
/// Mọi luật "ai được thao tác trên đối tượng nào" được đặt tại đây để có thể thay đổi ở một chỗ (B-02).
/// Người dùng truyền vào phải được nạp kèm Roles và Permissions.
/// </summary>
public interface IAccessPolicy
{
    /// <summary>Người dùng thuộc vai trò Quản trị hệ thống.</summary>
    bool IsAdministrator(PartyMemberProfile user);

    /// <summary>Người dùng có phạm vi nghiệp vụ đánh giá cấp cao (thẩm định, phê duyệt hoặc quản trị).</summary>
    bool HasElevatedEvaluationScope(PartyMemberProfile user);

    /// <summary>Kiểm tra quyền trên hồ sơ đánh giá cá nhân.</summary>
    bool CanAccessRecord(PartyMemberProfile user, EvaluationRecord record, AccessOperation operation);

    /// <summary>Kiểm tra quyền trên dữ liệu/báo cáo theo Chi bộ trong một kỳ; <paramref name="partyCellId"/> null nghĩa là toàn Đảng bộ.</summary>
    bool CanAccessBranch(PartyMemberProfile user, Guid? partyCellId, AccessOperation operation);

    /// <summary>Người dùng được xem hồ sơ tập thể/biên bản hội nghị (chưa xét phạm vi tổ chức).</summary>
    bool CanReadEvaluationDocuments(PartyMemberProfile user);

    /// <summary>Người dùng được lập hồ sơ tập thể/biên bản hội nghị (chưa xét phạm vi tổ chức).</summary>
    bool CanWriteEvaluationDocuments(PartyMemberProfile user);

    /// <summary>Kiểm tra quyền trên hồ sơ đánh giá tập thể (Mẫu 06-08) theo tổ chức gắn với hồ sơ.</summary>
    bool CanAccessCollective(PartyMemberProfile user, Guid? partyCellId, Guid? departmentId, AccessOperation operation);

    /// <summary>Kiểm tra quyền trên biên bản hội nghị (Mẫu 12-13) của một Chi bộ.</summary>
    bool CanAccessMeeting(PartyMemberProfile user, Guid? partyCellId, AccessOperation operation);

    /// <summary>
    /// Kiểm tra quyền trên tệp đính kèm. Quyền trên tệp bằng quyền trên các hồ sơ liên quan;
    /// tệp không gắn hồ sơ chỉ thuộc người tải lên và cấp quản trị, riêng văn bản chung (GENERAL)
    /// do quản trị tải lên thì mọi người đã đăng nhập được xem.
    /// </summary>
    /// <param name="user">Người yêu cầu.</param>
    /// <param name="attachment">Tệp cần kiểm tra.</param>
    /// <param name="links">Các hồ sơ đánh giá mà tệp đang gắn vào.</param>
    /// <param name="uploadedByAdministrator">Người tải lên thuộc vai trò Quản trị hệ thống.</param>
    /// <param name="operation">Thao tác.</param>
    bool CanAccessAttachment(
        PartyMemberProfile user,
        TaskAttachment attachment,
        IReadOnlyCollection<AttachmentRecordLink> links,
        bool uploadedByAdministrator,
        AccessOperation operation);

    /// <summary>Kiểm tra quyền trên hồ sơ người dùng (PartyMemberProfile) của người khác hoặc của chính mình.</summary>
    bool CanAccessProfile(PartyMemberProfile user, PartyMemberProfile target, AccessOperation operation);
}

/// <summary>Liên kết giữa tệp đính kèm và một hồ sơ đánh giá.</summary>
/// <param name="Record">Hồ sơ đánh giá (nạp kèm Member).</param>
/// <param name="ViaTask">
/// true nếu liên kết qua nhiệm vụ (EvaluationTask.AttachmentId, do người dùng tự chọn khi đăng ký/tự chấm);
/// false nếu qua TaskAttachment.RecordId.
/// </param>
public sealed record AttachmentRecordLink(EvaluationRecord Record, bool ViaTask);

/// <summary>Tiện ích tra cứu role/permission trên người dùng đã nạp kèm Roles và Permissions.</summary>
public static class UserAccessExtensions
{
    /// <summary>Người dùng có quyền nguyên tử thông qua các role đang được gán.</summary>
    public static bool HasPermission(this PartyMemberProfile user, string permissionCode)
    {
        return user.Roles.SelectMany(role => role.Permissions).Any(permission => permission.Code == permissionCode);
    }

    /// <summary>Người dùng có role hệ thống cụ thể.</summary>
    public static bool HasRole(this PartyMemberProfile user, string roleCode)
    {
        return user.Roles.Any(role => role.Code == roleCode);
    }
}

/// <summary>Triển khai mặc định của <see cref="IAccessPolicy"/>, giữ nguyên luật phạm vi đang áp dụng.</summary>
public sealed class AccessPolicy : IAccessPolicy
{
    /// <summary>Mã biểu mẫu của văn bản chung (không gắn hồ sơ).</summary>
    public const string GeneralFormCode = "GENERAL";

    /// <inheritdoc />
    public bool IsAdministrator(PartyMemberProfile user) => user.HasRole(AppRoles.QUAN_TRI_HE_THONG);

    /// <inheritdoc />
    public bool HasElevatedEvaluationScope(PartyMemberProfile user)
    {
        return user.HasPermission(AppPermissions.EvaluationsAppraise)
            || user.HasPermission(AppPermissions.EvaluationsApprove)
            || IsAdministrator(user);
    }

    /// <inheritdoc />
    public bool CanAccessRecord(PartyMemberProfile user, EvaluationRecord record, AccessOperation operation)
    {
        return operation switch
        {
            AccessOperation.Read or AccessOperation.Export => CanReadRecord(user, record),
            // Chỉ chủ hồ sơ được tự đăng ký/tự chấm điểm trên hồ sơ của mình.
            AccessOperation.Update => user.Id == record.MemberId,
            AccessOperation.BranchReview => user.HasPermission(AppPermissions.EvaluationsBranchVote)
                && user.PartyCellId.HasValue
                && user.PartyCellId == record.PartyCellId,
            AccessOperation.Approve => user.HasPermission(AppPermissions.EvaluationsApprove)
                && user.HasRole(record.Member?.IsApprovedByAttech == true
                    ? AppRoles.DANG_UY_CO_SO
                    : AppRoles.BAN_THUONG_VU),
            // Hệ thống chưa có chức năng xóa hồ sơ đánh giá.
            _ => false
        };
    }

    /// <summary>Luật xem hồ sơ theo cá nhân, Chi bộ hoặc quyền nghiệp vụ cấp cao (giữ nguyên từ EvaluationService.CanReadRecord).</summary>
    private static bool CanReadRecord(PartyMemberProfile user, EvaluationRecord record)
    {
        if (user.HasRole(AppRoles.QUAN_TRI_HE_THONG)
            || user.HasPermission(AppPermissions.EvaluationsAppraise)
            || (user.HasRole(AppRoles.BAN_THUONG_VU)
                && user.HasPermission(AppPermissions.EvaluationsApprove)))
            return true;

        if (user.HasRole(AppRoles.DANG_UY_CO_SO)
            && user.HasPermission(AppPermissions.EvaluationsApprove))
            return record.Member?.IsApprovedByAttech == true;

        if (user.HasPermission(AppPermissions.EvaluationsBranchVote))
            return user.PartyCellId.HasValue && user.PartyCellId == record.PartyCellId;

        return user.Id == record.MemberId;
    }

    /// <inheritdoc />
    public bool CanAccessBranch(PartyMemberProfile user, Guid? partyCellId, AccessOperation operation)
    {
        var hasCell = partyCellId.HasValue && partyCellId.Value != Guid.Empty;
        return operation switch
        {
            // Tra cứu hồ sơ theo Chi bộ: cấp cao xem mọi Chi bộ, người khác chỉ Chi bộ của mình.
            AccessOperation.Read => HasElevatedEvaluationScope(user)
                || (hasCell && user.PartyCellId == partyCellId),
            // Xuất biểu mẫu/báo cáo theo Chi bộ: cấp cao xem mọi phạm vi, Chi bộ chỉ Chi bộ của mình.
            AccessOperation.Export => HasElevatedEvaluationScope(user)
                || (hasCell
                    && user.HasPermission(AppPermissions.EvaluationsBranchVote)
                    && user.PartyCellId == partyCellId),
            AccessOperation.BranchReview => hasCell
                && user.HasPermission(AppPermissions.EvaluationsBranchVote)
                && user.PartyCellId.HasValue
                && user.PartyCellId == partyCellId,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool CanAccessCollective(PartyMemberProfile user, Guid? partyCellId, Guid? departmentId, AccessOperation operation)
    {
        var inScope = HasElevatedEvaluationScope(user)
            || (partyCellId.HasValue && partyCellId == user.PartyCellId)
            || (departmentId.HasValue && departmentId == user.DepartmentId);

        return operation switch
        {
            AccessOperation.Read => CanReadEvaluationDocuments(user) && inScope,
            AccessOperation.Update => CanWriteEvaluationDocuments(user) && inScope,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool CanAccessMeeting(PartyMemberProfile user, Guid? partyCellId, AccessOperation operation)
    {
        var inScope = HasElevatedEvaluationScope(user) || partyCellId == user.PartyCellId;
        return operation switch
        {
            AccessOperation.Read => CanReadEvaluationDocuments(user) && inScope,
            AccessOperation.Update => CanWriteEvaluationDocuments(user) && inScope,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool CanReadEvaluationDocuments(PartyMemberProfile user)
    {
        return HasElevatedEvaluationScope(user) || user.HasPermission(AppPermissions.EvaluationsBranchVote);
    }

    /// <inheritdoc />
    /// <remarks>Lập hồ sơ tập thể/biên bản cần quyền nghiệp vụ đánh giá; Quản trị hệ thống chỉ quản lý kỹ thuật.</remarks>
    public bool CanWriteEvaluationDocuments(PartyMemberProfile user)
    {
        return user.HasPermission(AppPermissions.EvaluationsBranchVote)
            || user.HasPermission(AppPermissions.EvaluationsAppraise)
            || user.HasPermission(AppPermissions.EvaluationsApprove);
    }

    /// <inheritdoc />
    public bool CanAccessAttachment(
        PartyMemberProfile user,
        TaskAttachment attachment,
        IReadOnlyCollection<AttachmentRecordLink> links,
        bool uploadedByAdministrator,
        AccessOperation operation)
    {
        if (IsAdministrator(user))
            return true;

        if (attachment.UploadedById.HasValue && attachment.UploadedById == user.Id)
            return true;

        // Liên kết qua nhiệm vụ do người dùng tự khai báo AttachmentId, nên chỉ được tính khi tệp là của
        // chính chủ hồ sơ (hoặc dữ liệu cũ chưa có người tải lên) — tránh gắn tệp của người khác vào hồ sơ mình để đọc.
        var effectiveLinks = links
            .Where(link => !link.ViaTask
                || !attachment.UploadedById.HasValue
                || attachment.UploadedById == link.Record.MemberId)
            .ToList();

        if (effectiveLinks.Count > 0)
        {
            // Xem/tải tệp theo quyền xem hồ sơ; sửa/xóa tệp theo quyền cập nhật hồ sơ.
            var recordOperation = operation is AccessOperation.Read or AccessOperation.Export
                ? AccessOperation.Read
                : AccessOperation.Update;
            return effectiveLinks.Any(link => CanAccessRecord(user, link.Record, recordOperation));
        }

        // Văn bản chung do quản trị tải lên: mọi người đã đăng nhập được xem.
        return operation is AccessOperation.Read or AccessOperation.Export
            && uploadedByAdministrator
            && string.Equals(attachment.FormCode, GeneralFormCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool CanAccessProfile(PartyMemberProfile user, PartyMemberProfile target, AccessOperation operation)
    {
        if (user.Id == target.Id)
            return operation == AccessOperation.Read;

        return operation switch
        {
            AccessOperation.Read => IsAdministrator(user) && user.HasPermission(AppPermissions.UsersRead),
            AccessOperation.Update => IsAdministrator(user) && user.HasPermission(AppPermissions.UsersUpdate),
            AccessOperation.Delete => IsAdministrator(user) && user.HasPermission(AppPermissions.UsersDelete),
            _ => false
        };
    }
}
