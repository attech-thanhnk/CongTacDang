import { API_BASE_URL, apiClient, request } from "./apiClient";

export interface AttachmentItem {
  id: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: string;
  uploadedBy: string;
  category: string;
  description: string;
  checksum?: string;
}

export const CATEGORY_MAP: Record<string, string> = {
  GENERAL: "Tài liệu chung",
  EVIDENCE: "Minh chứng hoàn thành",
  FORM: "Biểu mẫu đã ký",
  DECISION: "Nghị quyết & Quyết định",
};

export const attachmentService = {
  // Lấy danh sách toàn bộ tệp đính kèm
  async getAttachments(): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>("/attachments/list");
  },

  // Tải lên tệp đính kèm kèm phân loại và mô tả
  async uploadAttachment(file: File, category: string, description: string): Promise<any> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", category);
    formData.append("description", description);

    return apiClient.post("/attachments/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Cập nhật danh mục hoặc mô tả tệp
  async updateAttachment(id: string, category: string, description: string): Promise<any> {
    return request(`/attachments/${id}`, {
      method: "PUT",
      body: JSON.stringify({ category, description }),
    });
  },

  // Xóa tệp đính kèm theo Id
  async deleteAttachment(id: string): Promise<any> {
    return request(`/attachments/${id}`, {
      method: "DELETE",
    });
  },

  // Mở liên kết tải trực tiếp tệp trên tab mới
  downloadAttachment(id: string): void {
    window.open(`${API_BASE_URL}/attachments/${id}/download`, "_blank");
  },

  // Lấy đường dẫn API tải tệp
  getDownloadUrl(id: string): string {
    return `${API_BASE_URL}/attachments/${id}/download`;
  },

  // Định dạng dung lượng tệp sang chuỗi hiển thị (Bytes, KB, MB, GB)
  formatFileSize(bytes: number): string {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  },

  formatBytes(bytes: number): string {
    return this.formatFileSize(bytes);
  },

  // Chuyển mã phân loại sang tên hiển thị tiếng Việt
  getCategoryName(code: string): string {
    return CATEGORY_MAP[code] || code;
  },

  getCategoryLabel(code: string): string {
    return this.getCategoryName(code);
  },
};
