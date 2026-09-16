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

        // Định danh vị trí vật lý trong Storage Adapter (VD: general/202609/abc.pdf)
        public string ObjectKey { get; set; } = string.Empty;
        public string FilePath 
        { 
            get => ObjectKey; 
            set => ObjectKey = value; 
        }

        // Hạ tầng lưu trữ: "local" hoặc "minio"
        public string Provider { get; set; } = "local";

        // Mã băm SHA-256 để kiểm tra tính toàn vẹn và chống trùng lặp (Deduplication)
        public string Checksum { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;

        // Dẫn chiếu tới nghiệp vụ (related_type = FormCode, related_id = RelatedId/TaskId)
        public string FormCode { get; set; } = "GENERAL"; // GENERAL, EVIDENCE, FORM, DECISION
        public Guid? RelatedId { get; set; }
        public Guid? TaskId { get => RelatedId; set => RelatedId = value; }
        public Guid? RecordId { get; set; }
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
