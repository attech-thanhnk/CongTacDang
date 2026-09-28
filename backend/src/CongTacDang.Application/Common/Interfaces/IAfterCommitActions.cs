using System;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Hoãn tác vụ phụ (ví dụ xóa cache) tới khi transaction đang mở của đơn vị công việc kết thúc, để request chen giữa
/// không nạp lại dữ liệu cũ (chưa commit) vào cache. Không có transaction đang mở → chạy ngay.
/// </summary>
public interface IAfterCommitActions
{
    /// <summary>Chạy <paramref name="action"/> ngay, hoặc sau khi transaction đang mở kết thúc (commit hay rollback).</summary>
    void Run(Action action);
}
