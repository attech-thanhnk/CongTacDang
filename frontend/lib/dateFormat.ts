/**
 * Định dạng ngày giờ hiển thị dùng chung toàn ứng dụng: luôn dd/MM/yyyy (và HH:mm), không phụ thuộc ngôn ngữ trình duyệt.
 * Ghi rõ từng thành phần thay vì `dateStyle: "short"` vì mẫu "short" của locale vi có thể là d/M/yy tùy phiên bản ICU.
 * Chỉ dùng cho chỗ HIỂN THỊ; ô nhập `type="date"` vẫn theo trình duyệt.
 */
const dateFormatter = new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
const timeFormatter = new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false });

function toDate(value?: string | Date | null): Date | null {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** dd/MM/yyyy; trả về `fallback` (mặc định "—") khi trống hoặc không hợp lệ. */
export function formatDate(value?: string | Date | null, fallback = "—"): string {
  const date = toDate(value);
  return date ? dateFormatter.format(date) : fallback;
}

/** dd/MM/yyyy HH:mm; trả về `fallback` (mặc định "—") khi trống hoặc không hợp lệ. */
export function formatDateTime(value?: string | Date | null, fallback = "—"): string {
  const date = toDate(value);
  return date ? `${dateFormatter.format(date)} ${timeFormatter.format(date)}` : fallback;
}
