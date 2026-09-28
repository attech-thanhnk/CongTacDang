import { ApiError } from "@/services/apiClient";

/** Thông báo lỗi lấy từ máy chủ (ApiResponse.message) hoặc thông báo mặc định. */
export function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

/** Mã HTTP của lỗi API (nếu có). */
export function errorStatus(error: unknown): number | undefined {
  return error instanceof ApiError ? error.status : undefined;
}

/** Tiêu đề toast theo mã lỗi — 409 là chốt chặn nghiệp vụ, 403 là thiếu quyền. */
export function errorTitle(error: unknown): string | undefined {
  switch (errorStatus(error)) {
    case 409:
      return "Không thể thực hiện";
    case 403:
      return "Không có quyền";
    case 400:
      return "Dữ liệu chưa hợp lệ";
    default:
      return undefined;
  }
}

const dateTimeFormatter = new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" });
const dateFormatter = new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" });

export function formatDateTime(value?: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : dateTimeFormatter.format(date);
}

export function formatDate(value?: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : dateFormatter.format(date);
}

function pad(value: number): string {
  return String(value).padStart(2, "0");
}

/** Giá trị ISO → `yyyy-MM-dd` (giờ địa phương) cho `<input type="date">`. */
export function toDateInput(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** `yyyy-MM-dd` → ISO (UTC) của 00:00 giờ địa phương ngày đó. */
export function startOfDayIso(dateInput: string): string | undefined {
  if (!dateInput) return undefined;
  const date = new Date(`${dateInput}T00:00:00`);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

/**
 * "Đến hết ngày" `yyyy-MM-dd` → ISO của 00:00 ngày hôm sau (máy chủ coi `validTo` là mốc không bao gồm).
 */
export function endOfDayExclusiveIso(dateInput: string): string | undefined {
  if (!dateInput) return undefined;
  const date = new Date(`${dateInput}T00:00:00`);
  if (Number.isNaN(date.getTime())) return undefined;
  date.setDate(date.getDate() + 1);
  return date.toISOString();
}

/** Mốc `validTo` (không bao gồm) → ngày cuối cùng còn hiệu lực cho ô nhập "Đến hết ngày". */
export function exclusiveIsoToLastDayInput(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  date.setMilliseconds(date.getMilliseconds() - 1);
  return toDateInput(date.toISOString());
}

/**
 * Sao chép văn bản. `navigator.clipboard` chỉ có trong ngữ cảnh an toàn (HTTPS/localhost) —
 * hệ thống chạy trong mạng LAN nội bộ có thể dùng HTTP nên có phương án dự phòng.
 */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (typeof navigator !== "undefined" && navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // chuyển sang phương án dự phòng
  }
  try {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    const ok = document.execCommand("copy");
    document.body.removeChild(textarea);
    return ok;
  } catch {
    return false;
  }
}
