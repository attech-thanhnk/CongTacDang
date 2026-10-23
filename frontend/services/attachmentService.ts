import { apiClient, request } from "./apiClient";
import { downloadFile } from "./download";

export interface AttachmentItem {
  id: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: string;
  uploadedBy: string;
  formCode: string;
  description: string;
  checksum?: string;
}

/** Thao tác người dùng được làm với minh chứng của một hồ sơ (máy chủ tính theo quyền và giai đoạn của hồ sơ). */
export interface EvidenceRights {
  recordId: string;
  /** Open — thêm/thay/gỡ; AddOnly — chỉ bổ sung; Locked — khóa. */
  stage: "Open" | "AddOnly" | "Locked";
  canAdd: boolean;
  canReplace: boolean;
  canRemove: boolean;
  /** Lý do bị giới hạn (chỉ với chủ hồ sơ). */
  reason?: string | null;
}

/** Loại đối tượng gắn tệp (khớp AttachmentOwnerTypes ở máy chủ). */
export type AttachmentOwnerType = "EvaluationRecord" | "EvaluationTask" | "EvaluationAppeal" | "CollectiveEvaluationRecord";

// Tệp luôn gắn với một đối tượng (nhiệm vụ / hồ sơ đánh giá, kết quả của cấp trên); quyền xem tệp = quyền trên đối tượng.
export const attachmentService = {
  // Danh sách tệp người dùng được xem (tệp gắn hồ sơ trong phạm vi + tệp mình tải lên chưa gắn) — dùng khi chọn lại minh chứng
  async getAttachments(): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>("/attachments/list");
  },

  // Thông tin một tệp (kiểm tra quyền xem ở máy chủ)
  async getAttachment(id: string): Promise<AttachmentItem> {
    return request<AttachmentItem>(`/attachments/${id}`);
  },

  // Tải về tệp khi chỉ biết Id (lấy tên tệp từ máy chủ)
  async downloadById(id: string): Promise<void> {
    const info = await attachmentService.getAttachment(id);
    await attachmentService.downloadAttachment(id, info.fileName);
  },

  // Tải lên tệp minh chứng (chưa gắn): chỉ người tải lên thấy tới khi tệp được gắn vào nhiệm vụ / kết quả của cấp trên
  async uploadAttachment(file: File, formCode: string, description: string): Promise<AttachmentItem> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", formCode);
    formData.append("description", description);

    return request<AttachmentItem>("/attachments/upload", {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Tệp (phiên bản hiện hành) của một đối tượng mà người dùng được xem
  async getByOwner(ownerType: AttachmentOwnerType, ownerId: string): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>(`/attachments?ownerType=${ownerType}&ownerId=${ownerId}`);
  },

  // Thao tác được làm với minh chứng của hồ sơ (thêm / thay / gỡ)
  async getEvidenceRights(recordId: string): Promise<EvidenceRights> {
    return request<EvidenceRights>(`/attachments/evidence-rights?recordId=${recordId}`);
  },

  // Tải tệp lên và gắn ngay vào một đối tượng (máy chủ kiểm tra quyền trên đối tượng)
  async uploadToOwner(ownerType: AttachmentOwnerType, ownerId: string, file: File, formCode: string, description: string): Promise<AttachmentItem> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", formCode);
    formData.append("description", description);
    formData.append("ownerType", ownerType);
    formData.append("ownerId", ownerId);
    return request<AttachmentItem>("/attachments/upload", {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Tải tệp lên và gắn ngay vào hồ sơ đánh giá (máy chủ kiểm tra quyền trên hồ sơ)
  async uploadToRecord(recordId: string, file: File, formCode: string, description: string): Promise<AttachmentItem> {
    return attachmentService.uploadToOwner("EvaluationRecord", recordId, file, formCode, description);
  },

  // Thay tệp bằng phiên bản mới; phiên bản cũ vẫn được lưu
  async replaceVersion(id: string, file: File): Promise<AttachmentItem> {
    const formData = new FormData();
    formData.append("file", file);
    return request<AttachmentItem>(`/attachments/${id}/versions`, {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Gỡ tệp: minh chứng mình đã đính kèm vào hồ sơ của mình, hoặc phụ lục của hồ sơ tập thể (tệp vẫn được lưu, nhật ký ghi người gỡ)
  async remove(id: string): Promise<void> {
    await request<void>(`/attachments/${id}`, { method: "DELETE" });
  },

  // Tải tệp về máy qua API kèm xác thực (tên tệp theo máy chủ, không có thì dùng tên đã biết)
  async downloadAttachment(id: string, fileName?: string): Promise<void> {
    await downloadFile(`/attachments/${id}/download`, fileName || "tep-dinh-kem");
  },

  // Định dạng dung lượng tệp sang chuỗi hiển thị
  formatFileSize(bytes: number): string {
    if (!bytes || bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  },
};
