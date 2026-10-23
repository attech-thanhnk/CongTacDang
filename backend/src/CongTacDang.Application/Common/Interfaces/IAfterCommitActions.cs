using System;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Hoãn tác vụ phụ (vd. xóa cache) tới khi transaction đang mở kết thúc (commit hay rollback), để request chen giữa
/// không nạp dữ liệu chưa commit vào cache. Không có transaction → chạy ngay.
/// </summary>
public interface IAfterCommitActions
{
    void Run(Action action);
}
