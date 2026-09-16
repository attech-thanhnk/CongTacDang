using System;

namespace CongTacDang.Domain.Entities
{
    /// <summary>
    /// Thực thể lưu trữ tệp đính kèm minh chứng cho nhiệm vụ/giải trình (Mẫu 01, 02, 04, 10)
    /// </summary>
    public class TaskAttachment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;

        // Dẫn chiếu tới nhiệm vụ hoặc bản ghi đánh giá
        public Guid? TaskId { get; set; }
        public Guid? RecordId { get; set; }
        public string FormCode { get; set; } = "M01"; // M01, M02, M04, M10
        public string Description { get; set; } = string.Empty;
    }
}
