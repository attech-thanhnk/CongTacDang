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


// Ghi đè Content-Type JSON mặc định của apiClient (nếu không axios đổi FormData thành JSON); trình duyệt tự thêm boundary.
const postForm = <T>(url: string, form: FormData): Promise<T> =>
  apiClient.post(url, form, { headers: { "Content-Type": "multipart/form-data" }, timeout: 120000 }) as unknown as Promise<T>;

const fileForm = (file: File, fields: Record<string, string> = {}) => {
  const form = new FormData();
  form.append("file", file);
  for (const [key, value] of Object.entries(fields)) form.append(key, value);
  return form;
};

// Tệp luôn gắn với một đối tượng; quyền xem tệp = quyền trên đối tượng.
export const attachmentService = {
  async getAttachment(id: string): Promise<AttachmentItem> {
    return request<AttachmentItem>(`/attachments/${id}`);
  },

  async downloadById(id: string): Promise<void> {
    const info = await attachmentService.getAttachment(id);
    await attachmentService.downloadAttachment(id, info.fileName);
  },

  // Tệp chưa gắn chỉ người tải lên thấy, tới khi được gắn qua attachmentId khi lưu phiếu / kết quả
  async uploadAttachment(file: File, formCode: string, description: string): Promise<AttachmentItem> {
    return postForm<AttachmentItem>("/attachments/upload", fileForm(file, { formCode, description }));
  },

  async getByOwner(ownerType: AttachmentOwnerType, ownerId: string): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>(`/attachments?ownerType=${ownerType}&ownerId=${ownerId}`);
  },

  async getEvidenceRights(recordId: string): Promise<EvidenceRights> {
    return request<EvidenceRights>(`/attachments/evidence-rights?recordId=${recordId}`);
  },

  async uploadToOwner(ownerType: AttachmentOwnerType, ownerId: string, file: File, formCode: string, description: string): Promise<AttachmentItem> {
    return postForm<AttachmentItem>("/attachments/upload", fileForm(file, { formCode, description, ownerType, ownerId }));
  },

  async uploadToRecord(recordId: string, file: File, formCode: string, description: string): Promise<AttachmentItem> {
    return attachmentService.uploadToOwner("EvaluationRecord", recordId, file, formCode, description);
  },

  // Phiên bản cũ vẫn được lưu
  async replaceVersion(id: string, file: File): Promise<AttachmentItem> {
    return postForm<AttachmentItem>(`/attachments/${id}/versions`, fileForm(file));
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
};
