// Quy tắc tính thời hạn bước theo quý. Ngày cụ thể do máy chủ tính (backend/src/CongTacDang.Domain/Evaluation/DeadlineRules.cs).

export type DeadlineRuleKind = "WorkingDayOfQuarter" | "DayOfLastMonth";

export interface DeadlineRule {
  kind: DeadlineRuleKind;
  value: number;
}

export const DEADLINE_RULE_LABELS: Record<DeadlineRuleKind, string> = {
  WorkingDayOfQuarter: "Ngày làm việc thứ … đầu quý",
  DayOfLastMonth: "Ngày … tháng cuối quý",
};

export const DEADLINE_RULE_LIMITS: Record<DeadlineRuleKind, { min: number; max: number }> = {
  WorkingDayOfQuarter: { min: 1, max: 40 },
  DayOfLastMonth: { min: 1, max: 28 },
};
