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

        // Đường dẫn định danh tệp trong Storage (VD: general/202609/uuid_name.pdf)
        public string ObjectKey { get; set; } = string.Empty;
        public string FilePath 
        { 
            get => ObjectKey; 
            set => ObjectKey = value; 
        }

        // Mã băm SHA-256 kiểm tra toàn vẹn dữ liệu
        public string Checksum { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string UploadedBy { get; set; } = string.Empty;

        // Phân loại tài liệu và liên kết nghiệp vụ
        public string FormCode { get; set; } = "GENERAL";
        public Guid? RelatedId { get; set; }
        public Guid? TaskId { get => RelatedId; set => RelatedId = value; }
        public Guid? RecordId { get; set; }
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
