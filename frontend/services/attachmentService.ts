import { apiClient, request } from "./apiClient";

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
  async uploadAttachment(file: File, formCode: string, description: string): Promise<any> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", formCode);
    formData.append("description", description);

    return apiClient.post("/attachments/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Tải trực tiếp tệp an toàn qua API kèm xác thực
  async downloadAttachment(id: string, fileName?: string): Promise<void> {
    try {
      const res: any = await apiClient.get(`/attachments/${id}/download`, { responseType: "blob" });
      const actualBlob = res instanceof Blob ? res : new Blob([res]);
      const url = window.URL.createObjectURL(actualBlob);
      const link = document.createElement("a");
      link.href = url;
      if (fileName) link.setAttribute("download", fileName);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      console.error("Lỗi khi tải tệp tin:", err);
      throw err;
    }
  },

  // Tải nội dung blob để xem an toàn trong iframe / modal
  async getBlobUrl(id: string): Promise<{ url: string; mimeType: string }> {
    const res: any = await apiClient.get(`/attachments/${id}/download`, { responseType: "blob" });
    const actualBlob = res instanceof Blob ? res : new Blob([res], { type: res.type || "application/pdf" });
    return { url: window.URL.createObjectURL(actualBlob), mimeType: actualBlob.type.toLowerCase() };
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
