import type { AxiosRequestConfig, AxiosResponse } from "axios";
import { apiClient } from "./apiClient";

/** Tệp tải từ máy chủ: nội dung và tên tệp máy chủ đặt (null nếu không đọc được). */
export interface DownloadedFile {
  blob: Blob;
  fileName: string | null;
}

/**
 * Đọc tên tệp từ header `Content-Disposition`: ưu tiên `filename*=UTF-8''…` (RFC 5987, giữ được tiếng Việt),
 * sau đó `filename="…"`. Trả null khi không có.
 */
export function fileNameFromDisposition(header?: string | null): string | null {
  if (!header) return null;
  const extended = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(header);
  if (extended) {
    try {
      const name = decodeURIComponent(extended[2].trim().replace(/^"|"$/g, ""));
      if (name) return name;
    } catch {
      // mã hóa sai → dùng filename= bên dưới
    }
  }
  const plain = /filename\s*=\s*("([^"]*)"|[^;]+)/i.exec(header);
  const name = (plain?.[2] ?? plain?.[1] ?? "").trim();
  return name || null;
}

/** Lưu Blob thành tệp tải về. Thu hồi URL sau một nhịp để trình duyệt kịp bắt đầu tải. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.setAttribute("download", fileName);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
}

/** Lấy tệp từ API (kèm cookie xác thực), giữ header để đọc tên tệp. */
export async function fetchFile(url: string, config?: AxiosRequestConfig): Promise<DownloadedFile> {
  const response = (await apiClient.get<Blob>(url, { ...config, responseType: "blob", rawResponse: true })) as AxiosResponse<Blob>;
  const data: unknown = response.data;
  const blob = data instanceof Blob ? data : new Blob([data as BlobPart]);
  const disposition = response.headers?.["content-disposition"];
  return { blob, fileName: fileNameFromDisposition(typeof disposition === "string" ? disposition : null) };
}

/** Tải tệp về máy: tên tệp theo máy chủ, không có thì dùng `fallbackName`. */
export async function downloadFile(url: string, fallbackName: string, config?: AxiosRequestConfig): Promise<void> {
  const file = await fetchFile(url, config);
  saveBlob(file.blob, file.fileName || fallbackName);
}
