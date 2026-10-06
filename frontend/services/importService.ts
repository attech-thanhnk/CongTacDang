import { apiClient, request } from "./apiClient";

/** Cột của file nhập. */
export interface ImportColumn {
  key: string;
  header: string;
  required: boolean;
  description: string;
}

/** Loại nhập người dùng được dùng. */
export interface ImportKind {
  kind: string;
  name: string;
  description: string;
  columns: ImportColumn[];
}

export type ImportRowAction = "create" | "update" | "error";

/** Một dòng xem trước. */
export interface ImportPreviewRow {
  rowNumber: number;
  action: ImportRowAction;
  errors: string[];
  notes: string[];
  data: Record<string, string>;
}

/** Kết quả xem trước. */
export interface ImportPreview {
  sessionId: string;
  kind: string;
  fileName: string;
  expiresAt: string;
  columns: ImportColumn[];
  rows: ImportPreviewRow[];
  summary: { total: number; create: number; update: number; error: number };
}

/** Kết quả nhập. */
export interface ImportCommitResult {
  kind: string;
  total: number;
  created: number;
  updated: number;
  resultFileToken?: string | null;
  resultFileExpiresAt?: string | null;
}

/** Dung lượng tối đa (khớp máy chủ). */
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

/** Nhập dữ liệu từ Excel (API `/api/imports`, quyền system.import + quyền dữ liệu của từng loại). */
export const importService = {
  kinds(): Promise<ImportKind[]> {
    return request<ImportKind[]>("/imports/kinds");
  },

  async downloadTemplate(kind: string): Promise<void> {
    saveBlob(await apiClient.get(`/imports/${encodeURIComponent(kind)}/template`, { responseType: "blob" }), `mau-nhap-${kind}.xlsx`);
  },

  preview(kind: string, file: File): Promise<ImportPreview> {
    const form = new FormData();
    form.append("file", file);
    return apiClient.post(`/imports/${encodeURIComponent(kind)}/preview`, form, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 180000,
    }) as unknown as Promise<ImportPreview>;
  },

  commit(sessionId: string): Promise<ImportCommitResult> {
    // Nhập nhiều tài khoản mất thời gian (băm mật khẩu tạm) — chờ tối đa 10 phút.
    return apiClient.post(`/imports/sessions/${sessionId}/commit`, undefined, { timeout: 600000 }) as unknown as Promise<ImportCommitResult>;
  },

  /** Tải file mật khẩu tạm — máy chủ chỉ cho tải một lần. */
  async downloadResult(token: string, fileName: string): Promise<void> {
    saveBlob(await apiClient.get(`/imports/results/${encodeURIComponent(token)}`, { responseType: "blob" }), fileName);
  },
};
