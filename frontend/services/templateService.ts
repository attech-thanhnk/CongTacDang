import { apiClient, ApiError, request } from "./apiClient";
import { downloadFile } from "./download";

export interface TemplateTag {
  tag: string;
  kind: "field" | "repeat" | "if" | "ifnot";
  required: boolean;
  within?: string | null;
  isCommon: boolean;
}

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

export interface TemplateSummary {
  code: string;
  name: string;
  templateFileName: string;
  activeVersion?: TemplateVersion | null;
  versionCount: number;
  /** Tag bắt buộc mà phiên bản đang dùng không có. */
  activeMissingTags: string[];
  tags: TemplateTag[];
}

export interface TemplateCheck {
  isValid: boolean;
  tags: string[];
  errors: string[];
  warnings: string[];
  unknownTags: string[];
  missingRequiredTags: string[];
  version?: TemplateVersion | null;
}

/** Khớp máy chủ. */
export const TEMPLATE_MAX_FILE_BYTES = 10 * 1024 * 1024;

function fileForm(file: File, fields: Record<string, string> = {}): FormData {
  const form = new FormData();
  form.append("file", file);
  Object.entries(fields).forEach(([key, value]) => form.append(key, value));
  return form;
}

const base = (code: string) => `/templates/${encodeURIComponent(code)}`;

export const templateService = {
  list(): Promise<TemplateSummary[]> {
    return request<TemplateSummary[]>("/templates");
  },

  versions(code: string): Promise<TemplateVersion[]> {
    return request<TemplateVersion[]>(`${base(code)}/versions`);
  },

  /** Không lưu. */
  check(code: string, file: File): Promise<TemplateCheck> {
    return apiClient.post(`${base(code)}/check`, fileForm(file), {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 120000,
    }) as unknown as Promise<TemplateCheck>;
  },

  /** Tệp lỗi → 400 kèm kết quả kiểm tra: trả kết quả đó (isValid = false) thay vì ném lỗi, để hiện danh sách lỗi. */
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
    await downloadFile(`${base(code)}/current`, fileName);
  },

  async downloadOriginal(code: string, fileName: string): Promise<void> {
    await downloadFile(`${base(code)}/original`, fileName);
  },

  async downloadVersion(code: string, version: TemplateVersion, fileName: string): Promise<void> {
    await downloadFile(`${base(code)}/versions/${version.id}/file`, fileName);
  },
};
