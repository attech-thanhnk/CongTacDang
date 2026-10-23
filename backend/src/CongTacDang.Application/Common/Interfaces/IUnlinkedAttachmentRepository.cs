using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Một phiên bản tệp xét khi dọn tệp tải lên chưa gắn đối tượng.</summary>
/// <param name="GroupId"><c>FileGroupId ?? Id</c>.</param>
/// <param name="ObjectKey">Khóa tệp trên kho lưu trữ.</param>
public sealed record UnlinkedAttachmentVersion(Guid Id, Guid GroupId, string EffectiveOwnerType, DateTime UploadedAt, string ObjectKey);

/// <summary>
/// Tệp tải trước rồi gắn qua <c>AttachmentId</c>. "Đã gắn" = có đối tượng sở hữu, hoặc một phiên bản của nhóm
/// được nhiệm vụ (kể cả đã xóa mềm) / kết quả cấp trên tham chiếu.
/// </summary>
public interface IUnlinkedAttachmentRepository
{
    /// <summary>Số tệp (bản hiện hành) người dùng tải lên mà chưa gắn.</summary>
    Task<int> CountPendingUnlinkedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tối đa <paramref name="maxGroups"/> nhóm chưa gắn, mọi phiên bản tải lên trước <paramref name="uploadedBefore"/>.</summary>
    Task<List<UnlinkedAttachmentVersion>> GetCleanupCandidatesAsync(DateTime uploadedBefore, int maxGroups, CancellationToken ct = default);

    /// <summary>Id đang được nhiệm vụ hoặc kết quả cấp trên tham chiếu.</summary>
    Task<HashSet<Guid>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default);

    /// <summary>Xóa mềm bản ghi; tệp trên kho lưu trữ do bên gọi xóa.</summary>
    Task DeleteAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default);
}
