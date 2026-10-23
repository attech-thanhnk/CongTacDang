// Quy tắc tính thời hạn bước theo quý; ngày cụ thể do máy chủ tính.

export type DeadlineRuleKind = "WorkingDayOfQuarter" | "DayOfLastMonth" | "WorkingDaysBeforeQuarter";

export interface DeadlineRule {
  kind: DeadlineRuleKind;
  value: number;
}

export const DEADLINE_RULE_LABELS: Record<DeadlineRuleKind, string> = {
  WorkingDayOfQuarter: "Ngày làm việc thứ … đầu quý",
  DayOfLastMonth: "Ngày … tháng cuối quý",
  WorkingDaysBeforeQuarter: "… ngày làm việc trước đầu quý (cuối quý trước)",
};

export const DEADLINE_RULE_LIMITS: Record<DeadlineRuleKind, { min: number; max: number }> = {
  WorkingDayOfQuarter: { min: 1, max: 40 },
  DayOfLastMonth: { min: 1, max: 28 },
  WorkingDaysBeforeQuarter: { min: 1, max: 40 },
};
