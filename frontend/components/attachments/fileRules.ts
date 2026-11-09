import { formatFileSize } from "@/components/common/FileDropZone";

export { formatFileSize };

/** Khớp AllowedExtensions và MaxFileSize của AttachmentService ở máy chủ. */
export const ATTACH_EXTENSIONS = [".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png"];
export const ATTACH_ACCEPT = ATTACH_EXTENSIONS.join(",");
export const ATTACH_MAX_BYTES = 25 * 1024 * 1024;
export const ATTACH_HINT = "PDF, Word, Excel, JPG, PNG · tối đa 25 MB";

const extOf = (name: string) => {
  const dot = name.lastIndexOf(".");
  return dot < 0 ? "" : name.substring(dot).toLowerCase();
};

/** Kiểm tra trước khi gửi để báo lỗi ngay, máy chủ vẫn kiểm tra lại. */
export function checkAttachFile(file: File): string | null {
  if (!ATTACH_EXTENSIONS.includes(extOf(file.name)))
    return `Tệp "${file.name}" không đúng định dạng. Chỉ nhận PDF, Word (.docx), Excel (.xlsx), JPG, PNG.`;
  if (file.size === 0) return `Tệp "${file.name}" không có nội dung.`;
  if (file.size > ATTACH_MAX_BYTES) return `Tệp "${file.name}" lớn hơn 25 MB.`;
  return null;
}

/** Biểu tượng và màu theo loại tệp. */
export function fileKind(name: string): { icon: string; tone: "pdf" | "word" | "excel" | "image" | "other" } {
  switch (extOf(name)) {
    case ".pdf": return { icon: "fa-file-pdf", tone: "pdf" };
    case ".doc":
    case ".docx": return { icon: "fa-file-word", tone: "word" };
    case ".xls":
    case ".xlsx": return { icon: "fa-file-excel", tone: "excel" };
    case ".jpg":
    case ".jpeg":
    case ".png": return { icon: "fa-file-image", tone: "image" };
    default: return { icon: "fa-file", tone: "other" };
  }
}
