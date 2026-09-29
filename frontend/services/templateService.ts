import { apiClient, ApiError, request } from "./apiClient";

/** Một tag trong danh mục tag của biểu mẫu. */
export interface TemplateTag {
  tag: string;
  kind: "field" | "repeat" | "if" | "ifnot";
  required: boolean;
  within?: string | null;
  isCommon: boolean;
}

/** Một phiên bản file mẫu đã tải lên. */
export interface TemplateVersion {
  id: string;
  templateCode: string;
  versionNumber: number;
  originalFileName: string;
  fileSize: number;
  checksum: string;
  isActive: boolean;
  note?: string | null;
  tags: string[];
  warnings: string[];
  uploadedAt: string;
  uploadedByName?: string | null;
  activatedAt?: string | null;
  activatedByName?: string | null;
}

/** Một biểu mẫu Word trong danh mục. */
export interface TemplateSummary {
  code: string;
  name: string;
  templateFileName: string;
  activeVersion?: TemplateVersion | null;
  versionCount: number;
  tags: TemplateTag[];
}

/** Kết quả kiểm tra file mẫu. */
export interface TemplateCheck {
  isValid: boolean;
  tags: string[];
  errors: string[];
  warnings: string[];
  unknownTags: string[];
  missingRequiredTags: string[];
  version?: TemplateVersion | null;
}

/** Dung lượng tối đa (khớp máy chủ). */
export const TEMPLATE_MAX_FILE_BYTES = 10 * 1024 * 1024;

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

function fileForm(file: File, fields: Record<string, string> = {}): FormData {
  const form = new FormData();
  form.append("file", file);
  Object.entries(fields).forEach(([key, value]) => form.append(key, value));
  return form;
}

const base = (code: string) => `/templates/${encodeURIComponent(code)}`;

/** Quản lý file mẫu Word (API `/api/templates`, quyền system.templates.manage). */
export const templateService = {
  list(): Promise<TemplateSummary[]> {
    return request<TemplateSummary[]>("/templates");
  },

  versions(code: string): Promise<TemplateVersion[]> {
    return request<TemplateVersion[]>(`${base(code)}/versions`);
  },

  /** Kiểm tra tệp (không lưu). */
  check(code: string, file: File): Promise<TemplateCheck> {
    return apiClient.post(`${base(code)}/check`, fileForm(file), {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 120000,
    }) as unknown as Promise<TemplateCheck>;
  },

  /**
   * Tải lên phiên bản mới. Tệp có lỗi → máy chủ trả 400 kèm kết quả kiểm tra: trả về kết quả đó (isValid = false)
   * thay vì ném lỗi, để giao diện hiện danh sách lỗi.
   */
  async upload(code: string, file: File, note: string, activate: boolean): Promise<TemplateCheck> {
    try {
      return (await apiClient.post(`${base(code)}/versions`, fileForm(file, { note, activate: String(activate) }), {
        headers: { "Content-Type": "multipart/form-data" },
        timeout: 120000,
      })) as unknown as TemplateCheck;
    } catch (err) {
      const check = err instanceof ApiError ? (err.data?.data as TemplateCheck | undefined) : undefined;
      if (check && Array.isArray(check.errors)) return check;
      throw err;
    }
  },

  activate(code: string, versionId: string): Promise<TemplateVersion> {
    return request<TemplateVersion>(`${base(code)}/versions/${versionId}/activate`, { method: "POST" });
  },

  useOriginal(code: string): Promise<void> {
    return request<void>(`${base(code)}/use-original`, { method: "POST" });
  },

  async downloadCurrent(code: string, fileName: string): Promise<void> {
    saveBlob(await apiClient.get(`${base(code)}/current`, { responseType: "blob" }), fileName);
  },

  async downloadOriginal(code: string, fileName: string): Promise<void> {
    saveBlob(await apiClient.get(`${base(code)}/original`, { responseType: "blob" }), fileName);
  },

  async downloadVersion(code: string, version: TemplateVersion, fileName: string): Promise<void> {
    saveBlob(await apiClient.get(`${base(code)}/versions/${version.id}/file`, { responseType: "blob" }), fileName);
  },
};
