/** Mã nhóm "đã công bố" trong số liệu tiến độ (khớp máy chủ). */
export const PUBLISHED = "Published";

/** Trạng thái hồ sơ đang chờ từng bước, để mở danh sách hồ sơ đã lọc. */
export const STEP_STATUS: Record<string, string> = {
  B1_REGISTER: "AwaitingRegistration",
  B1_UNIT_SUBMIT: "AwaitingUnitSubmit",
  B1_HR_REVIEW: "AwaitingTaskReview",
  B1_APPROVE: "AwaitingTaskApproval",
  B2_SELF_SCORE: "AwaitingSelfScore",
  B2_CELL_CONFIRM: "AwaitingCellConfirm",
  B3A_COLLECTIVE: "AwaitingCollective",
  B3B_APPRAISAL: "AwaitingAppraisal",
  B3C_DIRECTOR: "AwaitingDirectorReview",
  B4_DECISION: "AwaitingDecision",
  B5_PUBLISH: "AwaitingPublish",
  [PUBLISHED]: "Published",
};

/** Màu theo thứ tự các bước đang có hồ sơ: nhạt (đầu luồng) → đậm (cuối luồng); đã công bố màu xanh lá. */
export function stepPalette(keys: string[]): Record<string, string> {
  const steps = keys.filter((k) => k !== PUBLISHED);
  const colors: Record<string, string> = { [PUBLISHED]: "var(--ok)" };
  steps.forEach((k, i) => {
    const pct = steps.length === 1 ? 70 : Math.round(30 + (i / (steps.length - 1)) * 70);
    colors[k] = `color-mix(in srgb, var(--primary) ${pct}%, var(--paper))`;
  });
  return colors;
}

/** "yyyy-MM-dd" → "dd/MM" (không đổi múi giờ). */
export function dayMonth(value?: string | null): string {
  if (!value) return "";
  const [, m, d] = value.slice(0, 10).split("-");
  return d && m ? `${d}/${m}` : "";
}

/** Hạn của bước: một ngày hoặc khoảng khi các diện quyết định khác hạn. */
export function deadlineRange(from?: string | null, to?: string | null): string {
  if (!from && !to) return "";
  if (!from || !to || from === to) return dayMonth(to || from);
  return `${dayMonth(from)}–${dayMonth(to)}`;
}
