import { request } from "./apiClient";

/** done | current | pending */
export type TimelineState = "done" | "current" | "pending";

export interface TimelineStepDto {
  step: string;
  name: string;
  /** yyyy-MM-dd; null khi bước không đặt hạn. */
  deadlineFrom?: string | null;
  deadlineTo?: string | null;
  state: TimelineState;
}

export interface PeriodTimelineDto {
  periodName: string;
  periodStatus: string;
  steps: TimelineStepDto[];
}

export interface MyLastResultDto {
  recordId: string;
  periodName: string;
  grade: string;
  score: number;
}

export interface ScopeStepCountDto {
  /** Mã bước, hoặc "Published". */
  step: string;
  name: string;
  count: number;
  overdue: number;
}

export interface LateRecordDto {
  recordId: string;
  fullName: string;
  step: string;
  stepName: string;
  performer: string;
  deadline: string;
  daysOverdue: number;
}

export interface ScopeGroupDto {
  partyCellId?: string | null;
  name: string;
  total: number;
  overdue: number;
  steps: ScopeStepCountDto[];
}

export interface ScopeProgressDto {
  total: number;
  published: number;
  overdue: number;
  dueSoon: number;
  dueSoonWorkingDays: number;
  steps: ScopeStepCountDto[];
  late: LateRecordDto[];
  groups: ScopeGroupDto[];
}

export interface DashboardOverviewDto {
  periodId: string;
  /** yyyy-MM-dd theo giờ Việt Nam. */
  today: string;
  timeline?: PeriodTimelineDto | null;
  lastResult?: MyLastResultDto | null;
  scope?: ScopeProgressDto | null;
}

export interface AdminAlertDto {
  code: string;
  label: string;
  count: number;
  link: string;
}

export interface AdminSummaryDto {
  items: AdminAlertDto[];
  usage?: { recentLogins: number; activeAccounts: number; days: number } | null;
}

export const dashboardService = {
  getOverview(periodId: string): Promise<DashboardOverviewDto> {
    return request<DashboardOverviewDto>(`/dashboard/overview?periodId=${encodeURIComponent(periodId)}`);
  },

  getAdmin(): Promise<AdminSummaryDto> {
    return request<AdminSummaryDto>("/dashboard/admin");
  },
};
