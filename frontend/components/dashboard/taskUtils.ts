import type { WorkQueueDto, WorkQueueItemDto, WorkflowStepCode } from "@/services/evaluationService";

/** Số ngày trước hạn coi là "sắp hạn". */
export const DUE_SOON_DAYS = 3;

export type DeadlineState = "overdue" | "soon" | "normal" | "none";

export function deadlineState(deadline?: string | null, overdue?: boolean): DeadlineState {
  if (overdue) return "overdue";
  if (!deadline) return "none";
  const due = new Date(deadline).getTime();
  if (Number.isNaN(due)) return "none";
  const days = (due - Date.now()) / 86_400_000;
  // Quá hạn do máy chủ xác định; hạn là ngày hôm nay (days < 0 theo giờ) vẫn tính là sắp hạn.
  return days <= DUE_SOON_DAYS ? "soon" : "normal";
}

export interface FlatTask extends WorkQueueItemDto {
  step: WorkflowStepCode;
  stepName: string;
}

/** Gộp việc của mọi bước, quá hạn trước rồi theo hạn gần nhất. */
export function flattenQueue(queue: WorkQueueDto | null): FlatTask[] {
  if (!queue) return [];
  const list = queue.groups.flatMap((g) => g.items.map((item) => ({ ...item, step: g.step, stepName: g.stepName })));
  const time = (t: FlatTask) => (t.deadline ? new Date(t.deadline).getTime() : Number.MAX_SAFE_INTEGER);
  return list.sort((a, b) => Number(b.overdue) - Number(a.overdue) || time(a) - time(b));
}

/** So khớp tên không phân biệt hoa thường và dấu tiếng Việt. */
export function normalizeText(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .trim();
}

/** Tên nút theo bước đang chờ; bước do cấp trên thực hiện: ghi nhận kết quả; bị trả lại: xem lại. */
const STEP_ACTIONS: Record<WorkflowStepCode, string> = {
  B1_REGISTER: "Đăng ký",
  B1_APPROVE: "Duyệt",
  B2_SELF_SCORE: "Tự chấm",
  B2_CELL_CONFIRM: "Xác nhận",
  B3A_COLLECTIVE: "Đề xuất",
  B3B_APPRAISAL: "Thẩm định",
  B3C_DIRECTOR: "Nhận xét",
  B4_DECISION: "Quyết định",
  B5_PUBLISH: "Công bố",
};

export function taskActionLabel(task: { step: WorkflowStepCode; mode: string; returnReason?: string | null }): string {
  if (task.returnReason) return "Xem lại";
  if (task.mode === "External") return "Ghi nhận";
  return STEP_ACTIONS[task.step] ?? "Xử lý";
}

/** Chữ cái đầu của hai từ cuối trong họ tên. */
export function initialsOf(fullName?: string | null): string {
  if (!fullName) return "?";
  return fullName
    .split(" ")
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0])
    .join("")
    .toUpperCase();
}

/** Ngày ngắn: dd/MM trong năm nay, dd/MM/yyyy nếu khác năm. */
export function shortDate(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const dd = String(date.getDate()).padStart(2, "0");
  const mm = String(date.getMonth() + 1).padStart(2, "0");
  return date.getFullYear() === new Date().getFullYear() ? `${dd}/${mm}` : `${dd}/${mm}/${date.getFullYear()}`;
}
