import { apiClient, request } from "./apiClient";
import { downloadFile } from "./download";

export interface ImportColumn {
  key: string;
  header: string;
  required: boolean;
  description: string;
}

export interface ImportKind {
  kind: string;
  name: string;
  description: string;
  columns: ImportColumn[];
}

export type ImportRowAction = "create" | "update" | "error";

export interface ImportPreviewRow {
  rowNumber: number;
  action: ImportRowAction;
  errors: string[];
  notes: string[];
  data: Record<string, string>;
}

export interface ImportPreview {
  sessionId: string;
  kind: string;
  fileName: string;
  expiresAt: string;
  columns: ImportColumn[];
  rows: ImportPreviewRow[];
  summary: { total: number; create: number; update: number; error: number };
}

export interface ImportCommitResult {
  kind: string;
  total: number;
  created: number;
  updated: number;
  resultFileToken?: string | null;
  resultFileExpiresAt?: string | null;
}

/** Khớp máy chủ. */
export const IMPORT_MAX_FILE_BYTES = 5 * 1024 * 1024;

export const importService = {
  kinds(): Promise<ImportKind[]> {
    return request<ImportKind[]>("/imports/kinds");
  },

  async downloadTemplate(kind: string): Promise<void> {
    await downloadFile(`/imports/${encodeURIComponent(kind)}/template`, `mau-nhap-${kind}.xlsx`);
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

  /** Máy chủ chỉ cho tải một lần. */
  async downloadResult(token: string, fileName: string): Promise<void> {
    await downloadFile(`/imports/results/${encodeURIComponent(token)}`, fileName);
  },
};
