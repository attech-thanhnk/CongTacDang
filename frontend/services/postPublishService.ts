import { apiClient, request } from "./apiClient";
import { reportService, ReportFileFormat } from "./reportService";

/** Tệp gắn kiến nghị (tóm tắt). */
export interface AppealFileDto {
  id: string;
  fileName: string;
  fileSize: number;
  uploadedAt: string;
  uploadedBy: string;
}

// ---------------------------------------------------------------------------
// Công khai kết quả (T-86)
// ---------------------------------------------------------------------------

/** Một dòng kết quả đã công bố — chỉ thông tin công khai (không có chi tiết hồ sơ, minh chứng, ý kiến). */
export interface PublishedResultItemDto {
  /** Chỉ có khi người xem được xem hồ sơ (chủ hồ sơ / quyền xem hồ sơ trong phạm vi). */
  recordId?: string | null;
  periodId: string;
  periodName: string;
  fullName: string;
  positionTitle?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  partyCellId?: string | null;
  partyCellName?: string | null;
  finalGrade: string;
  finalGradeName: string;
  /** null khi bộ tiêu chí của kỳ không cho công khai điểm. */
  finalScore?: number | null;
  publishedAt?: string | null;
  /** Có kiến nghị đang xử lý → kết quả "đang xem xét". */
  underReview: boolean;
  isOwn: boolean;
}

export interface ResultFilterOptionDto {
  id: string;
  name: string;
}

export interface GradeCountDto {
  grade: string;
  gradeName: string;
  count: number;
}

export interface PublishedResultsDto {
  items: PublishedResultItemDto[];
  gradeCounts: GradeCountDto[];
  periods: ResultFilterOptionDto[];
  departments: ResultFilterOptionDto[];
  showsScores: boolean;
}

// ---------------------------------------------------------------------------
// Kiến nghị (T-87)
// ---------------------------------------------------------------------------

export type AppealStatus = "Submitted" | "UnderReview" | "Accepted" | "Rejected";

export interface AppealDto {
  id: string;
  version: number;
  recordId: string;
  submittedByName: string;
  submittedAt: string;
  content: string;
  concernedSteps: string[];
  concernedStepNames: string[];
  status: AppealStatus;
  statusName: string;
  reviewStartedByName?: string | null;
  reviewStartedAt?: string | null;
  resolvedByName?: string | null;
  resolvedAt?: string | null;
  response?: string | null;
  reopenedAt?: string | null;
  canResolve: boolean;
  resolveBlockedReason?: string | null;
  canReopen: boolean;
  canAttach: boolean;
}

export interface AppealStepOptionDto {
  step: string;
  stepName: string;
  actorName?: string | null;
}

export interface RecordAppealsDto {
  recordId: string;
  underReview: boolean;
  canSubmit: boolean;
  submitBlockedReason?: string | null;
  stepOptions: AppealStepOptionDto[];
  appeals: AppealDto[];
}

// ---------------------------------------------------------------------------
// Kế hoạch 30-60-90 ngày — Mẫu 17 (T-88)
// ---------------------------------------------------------------------------

export type ImprovementPlanStatus = "Draft" | "Approved" | "Acknowledged" | "Closed";
export type MilestoneCode = "M30" | "M60" | "M90";
export type MilestoneResult = "Achieved" | "NotAchieved";

export interface ImprovementMilestoneDto {
  code: MilestoneCode;
  name: string;
  days: number;
  dueDate?: string | null;
  limitation?: string | null;
  target?: string | null;
  measures?: string | null;
  coordination?: string | null;
  result?: MilestoneResult | null;
  resultName: string;
  resultNote?: string | null;
  resultRecordedByName?: string | null;
  resultRecordedAt?: string | null;
}

export interface ImprovementPlanDto {
  id: string;
  version: number;
  recordId: string;
  status: ImprovementPlanStatus;
  statusName: string;
  startDate?: string | null;
  supporterName?: string | null;
  supporterTitle?: string | null;
  milestones: ImprovementMilestoneDto[];
  preparedByName?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
  acknowledgedAt?: string | null;
  acknowledgementComment?: string | null;
  closedAt?: string | null;
  createdAt: string;
}

export interface RecordImprovementPlanDto {
  recordId: string;
  isPublished: boolean;
  required: boolean;
  finalGrade: string;
  finalGradeName: string;
  requiredGradeNames: string[];
  plan?: ImprovementPlanDto | null;
  canManage: boolean;
  canAcknowledge: boolean;
}

export interface MilestoneInput {
  code: MilestoneCode;
  limitation?: string;
  target?: string;
  measures?: string;
  coordination?: string;
}

export interface SavePlanInput {
  version?: number;
  supporterName?: string;
  supporterTitle?: string;
  startDate?: string | null;
  milestones: MilestoneInput[];
}

/** Tên kết quả mốc đúng chữ trên Mẫu 17. */
export function milestoneResultLabel(code: MilestoneCode, result: MilestoneResult): string {
  if (code === "M90") return result === "Achieved" ? "Đạt (Đóng kế hoạch)" : "Không đạt (Xem xét nhân sự)";
  return result === "Achieved" ? "Đạt yêu cầu" : "Chưa chuyển biến";
}

// ---------------------------------------------------------------------------
// Nhắc việc (T-89)
// ---------------------------------------------------------------------------

export interface NotificationItemDto {
  kind: "overdue" | "dueSoon" | "appeal" | "improvementPlan" | "acknowledge" | "pending" | string;
  title: string;
  detail?: string | null;
  link: string;
  deadline?: string | null;
}

export interface NotificationSummaryDto {
  total: number;
  pendingSteps: number;
  dueSoon: number;
  overdue: number;
  appeals: number;
  improvementPlans: number;
  plansToAcknowledge: number;
  dueSoonDays: number;
  items: NotificationItemDto[];
}

const post = <T>(endpoint: string, body: unknown): Promise<T> =>
  request<T>(endpoint, { method: "POST", body: JSON.stringify(body ?? {}) });

const query = (params: Record<string, string | undefined>): string => {
  const parts = Object.entries(params)
    .filter(([, value]) => !!value)
    .map(([key, value]) => `${key}=${encodeURIComponent(value as string)}`);
  return parts.length ? `?${parts.join("&")}` : "";
};

/** API sau công bố: kết quả, kiến nghị, kế hoạch 30-60-90 ngày, nhắc việc. */
export const postPublishService = {
  getResults(filter: { periodId?: string; departmentId?: string; grade?: string }): Promise<PublishedResultsDto> {
    return request<PublishedResultsDto>(`/results${query(filter)}`);
  },

  getRecordAppeals(recordId: string): Promise<RecordAppealsDto> {
    return request<RecordAppealsDto>(`/evaluations/records/${recordId}/appeals`);
  },

  submitAppeal(recordId: string, content: string, concernedSteps: string[]): Promise<AppealDto> {
    return post<AppealDto>(`/evaluations/records/${recordId}/appeals`, { content, concernedSteps });
  },

  /** Tệp minh chứng gắn kiến nghị (đối tượng `EvaluationAppeal`) — chủ hồ sơ tải lên. */
  uploadAppealFile(appealId: string, file: File): Promise<unknown> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("formCode", "KN");
    formData.append("description", "Minh chứng kèm kiến nghị");
    formData.append("ownerType", "EvaluationAppeal");
    formData.append("ownerId", appealId);
    return apiClient.post("/attachments/upload", formData, { headers: { "Content-Type": "multipart/form-data" } });
  },

  getAppealFiles(appealId: string): Promise<AppealFileDto[]> {
    return request<AppealFileDto[]>(`/attachments${query({ ownerType: "EvaluationAppeal", ownerId: appealId })}`);
  },

  startReview(appealId: string, version: number): Promise<AppealDto> {
    return post<AppealDto>(`/appeals/${appealId}/start-review`, { version });
  },

  resolveAppeal(appealId: string, version: number, accepted: boolean, response: string): Promise<AppealDto> {
    return post<AppealDto>(`/appeals/${appealId}/resolve`, { version, accepted, response });
  },

  reopenFromAppeal(appealId: string, recordVersion: number, targetStep: string, reason: string): Promise<AppealDto> {
    return post<AppealDto>(`/appeals/${appealId}/reopen`, { recordVersion, targetStep, reason });
  },

  getRecordPlan(recordId: string): Promise<RecordImprovementPlanDto> {
    return request<RecordImprovementPlanDto>(`/evaluations/records/${recordId}/improvement-plan`);
  },

  createPlan(recordId: string, input: SavePlanInput): Promise<ImprovementPlanDto> {
    return post<ImprovementPlanDto>(`/evaluations/records/${recordId}/improvement-plan`, input);
  },

  updatePlan(planId: string, input: SavePlanInput): Promise<ImprovementPlanDto> {
    return request<ImprovementPlanDto>(`/improvement-plans/${planId}`, { method: "PUT", body: JSON.stringify(input) });
  },

  approvePlan(planId: string, version: number): Promise<ImprovementPlanDto> {
    return post<ImprovementPlanDto>(`/improvement-plans/${planId}/approve`, { version });
  },

  acknowledgePlan(planId: string, version: number, comment?: string): Promise<ImprovementPlanDto> {
    return post<ImprovementPlanDto>(`/improvement-plans/${planId}/acknowledge`, { version, comment });
  },

  recordMilestoneResult(planId: string, version: number, milestone: MilestoneCode, result: MilestoneResult, note?: string): Promise<ImprovementPlanDto> {
    return post<ImprovementPlanDto>(`/improvement-plans/${planId}/milestone-result`, { version, milestone, result, note });
  },

  /** Tải Mẫu 17 (Word hoặc PDF do máy chủ chuyển). */
  async downloadMau17(planId: string, fullName: string, format: ReportFileFormat = "original"): Promise<void> {
    const blob = await reportService.fetchReportBlob(`/improvement-plans/${planId}/mau-17`, format);
    const safeName = (fullName || "CanBo").trim().replace(/\s+/g, "_");
    reportService.saveBlob(blob, `Mau_17_KeHoach_30_60_90_${safeName}.${format === "pdf" ? "pdf" : "docx"}`);
  },

  getNotificationSummary(): Promise<NotificationSummaryDto> {
    return request<NotificationSummaryDto>("/notifications/summary");
  },
};
