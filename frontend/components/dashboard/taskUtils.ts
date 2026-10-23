import { OWNER_STEPS, STEP_NAMES } from "@/services/evaluationService";
import type { EvaluationRecordDto, RecordStepProgressDto, WorkQueueDto, WorkQueueItemDto } from "@/services/evaluationService";

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

/** Việc trong hàng đợi kèm mã nhóm: bước luồng hoặc nhóm việc khác (kiến nghị, kế hoạch, điều chỉnh việc). */
export interface TaskRow extends WorkQueueItemDto {
  step: string;
}

export interface TaskGroup {
  step: string;
  stepName: string;
  items: TaskRow[];
}

/** Nhóm là một bước của luồng đánh giá (khác nhóm việc sau công bố, điều chỉnh việc). */
export function isWorkflowStep(step: string): boolean {
  return step in STEP_NAMES;
}

/** Nhóm theo thứ tự máy chủ trả về; trong nhóm quá hạn trước rồi theo hạn gần nhất. */
export function queueGroups(queue: WorkQueueDto | null): TaskGroup[] {
  if (!queue) return [];
  const time = (t: TaskRow) => (t.deadline ? new Date(t.deadline).getTime() : Number.MAX_SAFE_INTEGER);
  return queue.groups
    .map((g) => ({
      step: g.step as string,
      stepName: g.stepName,
      items: g.items
        .map((item) => ({ ...item, step: g.step as string }))
        .sort((a, b) => Number(b.overdue) - Number(a.overdue) || time(a) - time(b)),
    }))
    .filter((g) => g.items.length > 0);
}

/** Hạn chung của cả nhóm (ghi một lần ở tiêu đề); undefined khi các việc khác hạn nhau. */
export function commonDeadline(items: TaskRow[]): { deadline: string | null; overdue: boolean } | undefined {
  const first = items[0];
  if (!first) return undefined;
  const same = items.every((i) => (i.deadline ?? null) === (first.deadline ?? null) && i.overdue === first.overdue);
  return same ? { deadline: first.deadline ?? null, overdue: first.overdue } : undefined;
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

/** Tên nút theo việc; dùng chung cho Tổng quan và Việc cần xử lý. */
const STEP_ACTIONS: Record<string, string> = {
  B1_REGISTER: "Đăng ký",
  B1_APPROVE: "Duyệt",
  B2_SELF_SCORE: "Tự chấm",
  B2_CELL_CONFIRM: "Xác nhận",
  B3A_COLLECTIVE: "Đề xuất",
  B3B_APPRAISAL: "Thẩm định",
  B3C_DIRECTOR: "Nhận xét",
  B4_DECISION: "Quyết định",
  B5_PUBLISH: "Công bố",
  TASK_CHANGES: "Duyệt điều chỉnh",
  APPEALS: "Xử lý kiến nghị",
  APPEALS_UNASSIGNED: "Gán vai trò",
  IMPROVEMENT_PLAN_ACK: "Xác nhận kế hoạch",
};

/** Bước do cấp trên thực hiện: nút ghi nhận kết quả của đúng bước đó. */
const EXTERNAL_ACTIONS: Record<string, string> = {
  B1_APPROVE: "Ghi nhận duyệt",
  B2_CELL_CONFIRM: "Ghi nhận xác nhận",
  B3A_COLLECTIVE: "Ghi nhận đề xuất",
  B3B_APPRAISAL: "Ghi nhận thẩm định",
  B3C_DIRECTOR: "Ghi nhận nhận xét",
  B4_DECISION: "Ghi nhận quyết định",
};

export function taskActionLabel(task: { step: string; mode: string; status: string }): string {
  if (task.step === "IMPROVEMENT_PLANS") return task.status === "PlanMissing" ? "Lập kế hoạch" : "Duyệt kế hoạch";
  if (task.mode === "External" && EXTERNAL_ACTIONS[task.step]) return EXTERNAL_ACTIONS[task.step];
  return STEP_ACTIONS[task.step] ?? "Mở hồ sơ";
}

/** Kiến nghị chưa có người xử lý: người gán vai trò có thể không xem được hồ sơ nên dẫn tới trang tài khoản. */
export function taskHref(task: { step: string; recordId: string }): string {
  return task.step === "APPEALS_UNASSIGNED" ? "/admin/users" : `/evaluations/${task.recordId}`;
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

/** Ngày dạng dd/MM/yyyy. */
export function shortDate(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const dd = String(date.getDate()).padStart(2, "0");
  const mm = String(date.getMonth() + 1).padStart(2, "0");
  return `${dd}/${mm}/${date.getFullYear()}`;
}

/** Bước hiện tại nếu là bước của chính chủ hồ sơ (đăng ký, tự chấm) — hạn đó là hạn của người xem. */
export function ownerCurrentStep(record: EvaluationRecordDto): RecordStepProgressDto | null {
  const current = record.progress?.find((s) => s.state === "current");
  return current && OWNER_STEPS.includes(current.step) ? current : null;
}

/** Mức quyết định; chưa có thì mức đề xuất gần nhất theo luồng. */
export function latestGrade(record: EvaluationRecordDto): { grade: string | null; final: boolean } {
  const set = (g?: string | null) => !!g && g !== "ChuaXepLoai";
  if (set(record.finalGrade)) return { grade: record.finalGrade, final: true };
  const proposed = [record.directorProposedGrade, record.appraisalProposedGrade, record.collectiveProposedGrade, record.selfProposedGrade].find(set);
  return { grade: proposed ?? null, final: false };
}
