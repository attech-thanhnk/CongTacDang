import { request } from "./apiClient";

export type ControlCheckStatus = "Draft" | "Submitted" | "Confirmed" | "Cancelled";
export type ControlCheckResult = "Pass" | "Fail" | "NotApplicable";
export type ControlCheckConclusion = "Eligible" | "NeedsWork";

export const CONTROL_STATUS_LABELS: Record<ControlCheckStatus, string> = {
  Draft: "Đang soạn",
  Submitted: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  Cancelled: "Đã hủy",
};

export const CONTROL_STATUS_TONES: Record<ControlCheckStatus, "success" | "info" | "warning" | "danger" | "neutral"> = {
  Draft: "info",
  Submitted: "warning",
  Confirmed: "success",
  Cancelled: "neutral",
};

export const CONTROL_RESULT_LABELS: Record<ControlCheckResult, string> = {
  Pass: "Đạt",
  Fail: "Chưa đạt",
  NotApplicable: "K/AD",
};

export const CONTROL_CONCLUSION_LABELS: Record<ControlCheckConclusion, string> = {
  Eligible: "Đủ điều kiện trình phê duyệt",
  NeedsWork: "Cần hoàn thiện",
};

export interface ControlCheckItemDto {
  itemCode: string;
  itemText: string;
  result?: ControlCheckResult | null;
  resultName?: string | null;
  evidence?: string | null;
}

export interface ControlCheckRecordDto {
  recordId: string;
  memberId: string;
  fullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  isCurrent: boolean;
  /** Hồ sơ này đã đổi sau lúc xác nhận nên cổng của riêng hồ sơ đang khóa. */
  isStale: boolean;
  gradeAtCheck: string;
  scoreAtCheck: number;
}

export interface ControlCheckHistoryDto {
  action: string;
  byUserName: string;
  at: string;
  reason?: string | null;
}

export interface ControlCheckSummaryDto {
  id: string;
  version: number;
  periodId: string;
  periodName: string;
  groupName: string;
  departmentId?: string | null;
  partyCellId?: string | null;
  status: ControlCheckStatus;
  statusName: string;
  conclusion?: ControlCheckConclusion | null;
  conclusionName?: string | null;
  revision: number;
  recordCount: number;
  performedByName: string;
  performedAt?: string | null;
  confirmedByName?: string | null;
  confirmedAt?: string | null;
  isStale: boolean;
  createdAt: string;
}

export interface ControlCheckDto extends ControlCheckSummaryDto {
  itemsVersion: number;
  needsWorkContent?: string | null;
  returnReason?: string | null;
  items: ControlCheckItemDto[];
  records: ControlCheckRecordDto[];
  history: ControlCheckHistoryDto[];
  canEdit: boolean;
  canSubmit: boolean;
  canConfirm: boolean;
  canReturn: boolean;
  canReopen: boolean;
  canCancel: boolean;
}

export interface CreateControlCheckRequest {
  periodId: string;
  groupName: string;
  departmentId?: string | null;
  partyCellId?: string | null;
  recordIds: string[];
}

export interface UpdateControlCheckRequest {
  version: number;
  groupName?: string | null;
  items?: { itemCode: string; result?: ControlCheckResult | null; evidence?: string | null }[];
  /** Chuỗi rỗng = bỏ kết luận. */
  conclusion?: ControlCheckConclusion | "" | null;
  needsWorkContent?: string | null;
  recordIds?: string[] | null;
}

const send = <T>(endpoint: string, method: "POST" | "PUT", body: unknown): Promise<T> =>
  request<T>(endpoint, { method, body: JSON.stringify(body ?? {}) });

export const controlCheckService = {
  list(periodId?: string): Promise<ControlCheckSummaryDto[]> {
    return request<ControlCheckSummaryDto[]>(`/control-checks${periodId ? `?periodId=${encodeURIComponent(periodId)}` : ""}`);
  },

  get(id: string): Promise<ControlCheckDto> {
    return request<ControlCheckDto>(`/control-checks/${id}`);
  },

  create(body: CreateControlCheckRequest): Promise<ControlCheckDto> {
    return send<ControlCheckDto>("/control-checks", "POST", body);
  },

  update(id: string, body: UpdateControlCheckRequest): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}`, "PUT", body);
  },

  submit(id: string, version: number): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}/submit`, "POST", { version });
  },

  confirm(id: string, version: number): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}/confirm`, "POST", { version });
  },

  returnBack(id: string, version: number, reason: string): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}/return`, "POST", { version, reason });
  },

  reopen(id: string, version: number, reason: string): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}/reopen`, "POST", { version, reason });
  },

  cancel(id: string, version: number, reason: string): Promise<ControlCheckDto> {
    return send<ControlCheckDto>(`/control-checks/${id}/cancel`, "POST", { version, reason });
  },
};

export const CONTROL_ACTION_LABELS: Record<string, string> = {
  Create: "Lập phiếu",
  Submit: "Gửi xác nhận",
  Confirm: "Xác nhận",
  Return: "Trả lại",
  Reopen: "Mở lại",
  Cancel: "Hủy phiếu",
};
