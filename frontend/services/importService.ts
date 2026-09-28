import { apiClient, request } from "./apiClient";

/** Cột của file mẫu — dùng để dựng bảng xem trước động. */
export interface ImportColumn {
  key: string;
  header: string;
  required: boolean;
  description: string;
  allowedValues?: string[] | null;
}

/** Một loại dữ liệu người dùng hiện tại được nhập. */
export interface ImportKind {
  kind: string;
  displayName: string;
  description: string;
  columns: ImportColumn[];
}

export type ImportRowAction = "create" | "update" | "error";

/** Kết quả kiểm tra một dòng. */
export interface ImportPreviewRow {
  rowNumber: number;
  action: ImportRowAction;
  errors: string[];
  data: Record<string, string>;
}

/** Kết quả bước xem trước. */
export interface ImportPreview {
  sessionId: string;
  kind: string;
  fileName?: string;
  expiresAt: string;
  canCommit: boolean;
  columns: ImportColumn[];
  rows: ImportPreviewRow[];
  summary: { total: number; create: number; update: number; error: number };
}

/** Kết quả bước xác nhận. */
export interface ImportCommitResult {
  created: number;
  updated: number;
  resultFileToken?: string | null;
  resultFileExpiresAt?: string | null;
}

/** Giới hạn dung lượng tệp (khớp máy chủ). */
export const IMPORT_MAX_FILE_BYTES = 5 * 1024 * 1024;

function saveBlob(data: unknown, fileName: string) {
  const blob = data instanceof Blob ? data : new Blob([data as BlobPart]);
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.setAttribute("download", fileName);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(url);
}

/** Dịch vụ nhập dữ liệu (API `/api/imports`). Không phụ thuộc loại cụ thể. */
export const importService = {
  /** Các loại người dùng được phép nhập. */
  getKinds(): Promise<ImportKind[]> {
    return request<ImportKind[]>("/imports/kinds");
  },

  /** Tải file mẫu. */
  async downloadTemplate(kind: string): Promise<void> {
    const data = await apiClient.get(`/imports/${encodeURIComponent(kind)}/template`, { responseType: "blob" });
    saveBlob(data, `mau-nhap-${kind}.xlsx`);
  },

  /** Tải tệp lên để xem trước. */
  preview(kind: string, file: File): Promise<ImportPreview> {
    const formData = new FormData();
    formData.append("file", file);
    return apiClient.post(`/imports/${encodeURIComponent(kind)}/preview`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 120000,
    }) as unknown as Promise<ImportPreview>;
  },

  /** Xác nhận ghi dữ liệu (tạo nhiều tài khoản có thể mất vài phút). */
  commit(sessionId: string): Promise<ImportCommitResult> {
    return apiClient.post(`/imports/${sessionId}/commit`, undefined, { timeout: 600000 }) as unknown as Promise<ImportCommitResult>;
  },

  /** Tải tệp kết quả — chỉ được một lần. */
  async downloadResult(token: string, fileName: string): Promise<void> {
    const data = await apiClient.get(`/imports/results/${encodeURIComponent(token)}`, { responseType: "blob" });
    saveBlob(data, fileName);
  },
};
