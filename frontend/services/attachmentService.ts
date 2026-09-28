import { API_BASE_URL, apiClient, request } from "./apiClient";

export interface AttachmentItem {
  id: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: string;
  uploadedBy: string;
  category: string; // Tương ứng FormCode trong CSDL
  formCode?: string;
  description: string;
  checksum?: string;
}

export const attachmentService = {
  // Lấy danh sách toàn bộ tệp đính kèm
  async getAttachments(): Promise<AttachmentItem[]> {
    return request<AttachmentItem[]>("/attachments/list");
  },

  // Tải lên tệp đính kèm kèm mã biểu mẫu và trích yếu
  async uploadAttachment(file: File, formCode: string, description: string): Promise<any> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", formCode || "GENERAL");
    formData.append("description", description);

    return apiClient.post("/attachments/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Xóa tệp đính kèm theo Id
  async deleteAttachment(id: string): Promise<any> {
    return request(`/attachments/${id}`, {
      method: "DELETE",
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

  // Lấy URL xem trực tiếp (inline)
  getViewUrl(id: string): string {
    return `${API_BASE_URL}/attachments/${id}/view`;
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
