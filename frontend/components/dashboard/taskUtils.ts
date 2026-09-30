import type { WorkQueueDto, WorkQueueItemDto } from "@/services/evaluationService";

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
  stepName: string;
}

/** Gộp việc của mọi bước, quá hạn trước rồi theo hạn gần nhất. */
export function flattenQueue(queue: WorkQueueDto | null): FlatTask[] {
  if (!queue) return [];
  const list = queue.groups.flatMap((g) => g.items.map((item) => ({ ...item, stepName: g.stepName })));
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
