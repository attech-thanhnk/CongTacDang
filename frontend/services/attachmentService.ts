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
  fileGroupId?: string;
  versionNumber?: number;
  isCurrent?: boolean;
}

/** Máy chủ tính theo quyền và giai đoạn của hồ sơ. */
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

/** Khớp AttachmentOwnerTypes ở máy chủ. */
export type AttachmentOwnerType =
  | "EvaluationRecord"
  | "EvaluationTask"
  | "EvaluationAppeal"
  | "CollectiveEvaluationRecord"
  | "ExpertCrosscheck"
  | "ControlCheck"
  | "CalibrationSession";

// Tệp luôn gắn với một đối tượng; quyền xem tệp = quyền trên đối tượng.
export const attachmentService = {
  // Tệp gắn hồ sơ trong phạm vi + tệp mình tải lên chưa gắn
  async getAttachments(): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>("/attachments/list");
  },

  async getAttachment(id: string): Promise<AttachmentItem> {
    return request<AttachmentItem>(`/attachments/${id}`);
  },

  async downloadById(id: string): Promise<void> {
    const info = await attachmentService.getAttachment(id);
    await attachmentService.downloadAttachment(id, info.fileName);
  },

  // Tệp chưa gắn chỉ người tải lên thấy
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

  async getByOwner(ownerType: AttachmentOwnerType, ownerId: string): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>(`/attachments?ownerType=${ownerType}&ownerId=${ownerId}`);
  },

  async getEvidenceRights(recordId: string): Promise<EvidenceRights> {
    return request<EvidenceRights>(`/attachments/evidence-rights?recordId=${recordId}`);
  },

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

  async uploadToRecord(recordId: string, file: File, formCode: string, description: string): Promise<AttachmentItem> {
    return attachmentService.uploadToOwner("EvaluationRecord", recordId, file, formCode, description);
  },

  // Phiên bản cũ vẫn được lưu
  async replaceVersion(id: string, file: File): Promise<AttachmentItem> {
    const formData = new FormData();
    formData.append("file", file);
    return request<AttachmentItem>(`/attachments/${id}/versions`, {
      method: "POST",
      body: formData,
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Tệp vẫn được lưu, nhật ký ghi người gỡ
  async remove(id: string): Promise<void> {
    await request<void>(`/attachments/${id}`, { method: "DELETE" });
  },

  // Mới nhất trước
  async getVersions(id: string): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>(`/attachments/${id}/versions`);
  },

  async downloadVersion(id: string, versionNumber: number, fileName?: string): Promise<void> {
    await downloadFile(`/attachments/${id}/versions/${versionNumber}/download`, fileName || "tep-dinh-kem");
  },

  // Tên tệp theo máy chủ, không có thì dùng tên đã biết
  async downloadAttachment(id: string, fileName?: string): Promise<void> {
    await downloadFile(`/attachments/${id}/download`, fileName || "tep-dinh-kem");
  },

  formatFileSize(bytes: number): string {
    if (!bytes || bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  },
};
