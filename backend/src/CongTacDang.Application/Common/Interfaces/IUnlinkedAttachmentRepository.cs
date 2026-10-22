using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Một phiên bản tệp xét khi dọn tệp tải lên chưa gắn đối tượng.</summary>
/// <param name="Id">Id phiên bản.</param>
/// <param name="GroupId">Nhóm phiên bản (<c>FileGroupId ?? Id</c>).</param>
/// <param name="EffectiveOwnerType">Loại đối tượng sở hữu thực tế (<c>TaskAttachment.EffectiveOwnerType</c>).</param>
/// <param name="UploadedAt">Thời điểm tải lên.</param>
/// <param name="ObjectKey">Khóa tệp trên kho lưu trữ.</param>
public sealed record UnlinkedAttachmentVersion(Guid Id, Guid GroupId, string EffectiveOwnerType, DateTime UploadedAt, string ObjectKey);

/// <summary>
/// Truy vấn tệp tải lên chưa gắn đối tượng (minh chứng / văn bản của cấp trên tải trước rồi gắn qua <c>AttachmentId</c>):
/// giới hạn số tệp đang chờ gắn của mỗi người và dọn tệp bỏ quên.
/// "Đã gắn" gồm: có loại đối tượng sở hữu (hồ sơ, nhiệm vụ, kiến nghị, hồ sơ tập thể; kể cả cột cũ RecordId/RelatedId), hoặc một
/// phiên bản bất kỳ của nhóm được <c>EvaluationTask.AttachmentId</c> / <c>EvaluationExternalResult.AttachmentId</c> tham chiếu
/// (kể cả nhiệm vụ đã xóa mềm).
/// </summary>
public interface IUnlinkedAttachmentRepository
{
    /// <summary>Số tệp (phiên bản hiện hành) người dùng đã tải lên mà chưa gắn vào đối tượng nào.</summary>
    Task<int> CountPendingUnlinkedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Mọi phiên bản (chưa xóa) của tối đa <paramref name="maxGroups"/> nhóm tệp chưa gắn, không được tham chiếu và mọi phiên bản
    /// tải lên trước <paramref name="uploadedBefore"/>.
    /// </summary>
    Task<List<UnlinkedAttachmentVersion>> GetCleanupCandidatesAsync(DateTime uploadedBefore, int maxGroups, CancellationToken ct = default);

    /// <summary>Các Id trong <paramref name="attachmentIds"/> đang được nhiệm vụ hoặc kết quả của cấp trên tham chiếu.</summary>
    Task<HashSet<Guid>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default);

    /// <summary>Xóa (mềm, ghi nhật ký) bản ghi các phiên bản tệp; tệp trên kho lưu trữ do bên gọi xóa.</summary>
    Task DeleteAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default);
}
