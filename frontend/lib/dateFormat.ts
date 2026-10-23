/**
 * Chỉ để hiển thị. Ghi rõ từng thành phần vì `dateStyle: "short"` của locale vi có thể là d/M/yy tùy phiên bản ICU.
 */
const dateFormatter = new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
const timeFormatter = new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false });

function toDate(value?: string | Date | null): Date | null {
  if (!value) return null;
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** dd/MM/yyyy; `fallback` khi trống hoặc không hợp lệ. */
export function formatDate(value?: string | Date | null, fallback = "—"): string {
  const date = toDate(value);
  return date ? dateFormatter.format(date) : fallback;
}

/** dd/MM/yyyy HH:mm; `fallback` khi trống hoặc không hợp lệ. */
export function formatDateTime(value?: string | Date | null, fallback = "—"): string {
  const date = toDate(value);
  return date ? `${dateFormatter.format(date)} ${timeFormatter.format(date)}` : fallback;
}
