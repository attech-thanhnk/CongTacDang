import { request } from "./apiClient";

export type CalibrationStatus = "Draft" | "Concluded" | "Void";

export const CALIBRATION_STATUS_LABELS: Record<CalibrationStatus, string> = {
  Draft: "Đang soạn",
  Concluded: "Đã chốt",
  Void: "Đã hủy",
};

export const CALIBRATION_STATUS_TONES: Record<CalibrationStatus, "success" | "info" | "warning" | "danger" | "neutral"> = {
  Draft: "info",
  Concluded: "success",
  Void: "neutral",
};

export interface CalibrationSessionListItemDto {
  id: string;
  version: number;
  periodId: string;
  periodName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  groupName: string;
  status: CalibrationStatus;
  statusName: string;
  heldAt?: string | null;
  recordCount: number;
  preparedByName: string;
  concludedAt?: string | null;
}

export interface CalibrationRecordDto {
  recordId: string;
  memberId: string;
  fullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  workflowProfileCode: string;
  recordStatus: string;
  gradeBefore: string;
  scoreBefore?: number | null;
  selfScore?: number | null;
  selfGrade: string;
  collectiveGrade: string;
  appraisalScore?: number | null;
  appraisalGrade: string;
  calibratedScore?: number | null;
  calibratedGrade: string;
  snapshotOutdated: boolean;
}

export interface CalibrationAttendeeDto {
  order: number;
  fullName: string;
  memberId?: string | null;
  capacity?: string | null;
  dissentNote?: string | null;
}

export interface CalibrationSummaryRowDto {
  gradeCode: string;
  gradeName: string;
  countBefore: number;
  percentBefore?: number | null;
  countAfter: number;
  percentAfter?: number | null;
  notableDifference?: string | null;
  preliminaryConclusion?: string | null;
}

export interface CalibrationCaseDto {
  order: number;
  recordId: string;
  fullName: string;
  gradeBefore: string;
  scoreBefore?: number | null;
  clarification?: string | null;
  gradeAfter: string;
  scoreAfter?: number | null;
  basis?: string | null;
  responsibleName?: string | null;
  responsibleMemberId?: string | null;
  changed: boolean;
}

export interface CalibrationSessionDto {
  id: string;
  version: number;
  periodId: string;
  periodName: string;
  departmentId?: string | null;
  departmentName?: string | null;
  partyCellId?: string | null;
  partyCellName?: string | null;
  groupName: string;
  heldAt?: string | null;
  location?: string | null;
  chairName: string;
  chairMemberId?: string | null;
  secretaryName: string;
  secretaryMemberId?: string | null;
  scopeNote?: string | null;
  scopeSuggestion: string;
  extraConclusion?: string | null;
  status: CalibrationStatus;
  statusName: string;
  revision: number;
  preparedByName: string;
  preparedAt: string;
  concludedByName?: string | null;
  concludedAt?: string | null;
  reopenReason?: string | null;
  voidReason?: string | null;
  records: CalibrationRecordDto[];
  attendees: CalibrationAttendeeDto[];
  summary: CalibrationSummaryRowDto[];
  cases: CalibrationCaseDto[];
  canEdit: boolean;
  canConclude: boolean;
  canReopen: boolean;
  canVoid: boolean;
  concludeBlockedReason?: string | null;
}

export interface CalibrationCandidateDto {
  recordId: string;
  memberId: string;
  fullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  workflowProfileCode: string;
  workflowProfileName: string;
  recordStatus: string;
  gradeBefore: string;
  scoreBefore?: number | null;
}

export interface CalibrationHistoryDto {
  action: string;
  actionName: string;
  byUserName: string;
  at: string;
  reason?: string | null;
}

export interface CreateCalibrationRequest {
  periodId: string;
  departmentId?: string | null;
  partyCellId?: string | null;
  groupName?: string | null;
  recordIds: string[];
}

/** Lưu toàn bộ bản nháp: danh sách gửi lên thay thế danh sách cũ. */
export interface UpdateCalibrationRequest {
  version: number;
  groupName?: string | null;
  /** yyyy-MM-dd */
  heldAt?: string | null;
  location?: string | null;
  chairName?: string | null;
  chairMemberId?: string | null;
  secretaryName?: string | null;
  secretaryMemberId?: string | null;
  scopeNote?: string | null;
  extraConclusion?: string | null;
  recordIds: string[];
  attendees: { fullName?: string | null; memberId?: string | null; capacity?: string | null; dissentNote?: string | null }[];
  summary: { gradeCode?: string | null; notableDifference?: string | null; preliminaryConclusion?: string | null }[];
  cases: {
    recordId: string;
    clarification?: string | null;
    gradeAfter?: string | null;
    scoreAfter?: number | null;
    basis?: string | null;
    responsibleName?: string | null;
    responsibleMemberId?: string | null;
  }[];
}

const send = <T>(endpoint: string, method: "POST" | "PUT", body: unknown): Promise<T> =>
  request<T>(endpoint, { method, body: JSON.stringify(body ?? {}) });

export const calibrationService = {
  list(periodId?: string): Promise<CalibrationSessionListItemDto[]> {
    return request<CalibrationSessionListItemDto[]>(`/calibrations${periodId ? `?periodId=${encodeURIComponent(periodId)}` : ""}`);
  },

  candidates(periodId: string, departmentId?: string, workflowProfileCode?: string): Promise<CalibrationCandidateDto[]> {
    const query = new URLSearchParams({ periodId });
    if (departmentId) query.set("departmentId", departmentId);
    if (workflowProfileCode) query.set("workflowProfileCode", workflowProfileCode);
    return request<CalibrationCandidateDto[]>(`/calibrations/candidates?${query.toString()}`);
  },

  get(id: string): Promise<CalibrationSessionDto> {
    return request<CalibrationSessionDto>(`/calibrations/${id}`);
  },

  history(id: string): Promise<CalibrationHistoryDto[]> {
    return request<CalibrationHistoryDto[]>(`/calibrations/${id}/history`);
  },

  create(body: CreateCalibrationRequest): Promise<CalibrationSessionDto> {
    return send<CalibrationSessionDto>("/calibrations", "POST", body);
  },

  update(id: string, body: UpdateCalibrationRequest): Promise<CalibrationSessionDto> {
    return send<CalibrationSessionDto>(`/calibrations/${id}`, "PUT", body);
  },

  conclude(id: string, version: number): Promise<CalibrationSessionDto> {
    return send<CalibrationSessionDto>(`/calibrations/${id}/conclude`, "POST", { version });
  },

  reopen(id: string, version: number, reason: string): Promise<CalibrationSessionDto> {
    return send<CalibrationSessionDto>(`/calibrations/${id}/reopen`, "POST", { version, reason });
  },

  void(id: string, version: number, reason: string): Promise<CalibrationSessionDto> {
    return send<CalibrationSessionDto>(`/calibrations/${id}/void`, "POST", { version, reason });
  },
};
