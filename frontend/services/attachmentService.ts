import { API_BASE_URL, request } from "./apiClient";

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
  provider?: string;
}

export const CATEGORY_MAP: Record<string, string> = {
  GENERAL: "Tài liệu chung",
  EVIDENCE: "Minh chứng hoàn thành",
  FORM: "Biểu mẫu đã ký",
  DECISION: "Nghị quyết & Quyết định",
};

export const attachmentService = {
  async getAttachments(): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>("/attachments/list");
  },

  async uploadAttachment(file: File, category: string, description: string): Promise<any> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", category);
    formData.append("description", description);

    const response = await fetch(`${API_BASE_URL}/attachments/upload`, {
      method: "POST",
      body: formData,
    });

    if (!response.ok) {
      let errorMsg = "Lỗi tải tệp tin lên máy chủ.";
      try {
        const errJson = await response.json();
        if (errJson && errJson.message) errorMsg = errJson.message;
      } catch {
        const errText = await response.text();
        if (errText) errorMsg = errText;
      }
      throw new Error(errorMsg);
    }

    const res = await response.json();
    return res && res.data ? res.data : res;
  },

  async updateAttachment(id: string, category: string, description: string): Promise<any> {
    return request(`/attachments/${id}`, {
      method: "PUT",
      body: JSON.stringify({ category, description }),
    });
  },

  async deleteAttachment(id: string): Promise<any> {
    return request(`/attachments/${id}`, {
      method: "DELETE",
    });
  },

  downloadAttachment(id: string): void {
    window.open(`${API_BASE_URL}/attachments/${id}/download`, "_blank");
  },

  getDownloadUrl(id: string): string {
    return `${API_BASE_URL}/attachments/${id}/download`;
  },

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

  getCategoryName(code: string): string {
    return CATEGORY_MAP[code] || code;
  },

  getCategoryLabel(code: string): string {
    return this.getCategoryName(code);
  },
};
