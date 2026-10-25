import { request } from "./apiClient";

export type CrosscheckStatus = "Requested" | "InProgress" | "Submitted" | "Confirmed" | "Cancelled";
export type CrosscheckResult = "Pass" | "Exceed" | "Defect";
export type CrosscheckAdjustment = "None" | "Decrease" | "RecognizeExceed";

export const CROSSCHECK_FIELDS: { code: string; label: string }[] = [
  { code: "SAFETY_QUALITY", label: "An toàn - chất lượng" },
  { code: "CNS_TECHNICAL", label: "Kỹ thuật CNS" },
  { code: "ATM", label: "Quản lý không lưu" },
  { code: "PLANNING_PROJECT", label: "Kế hoạch - đầu tư / dự án" },
  { code: "FINANCE", label: "Tài chính" },
  { code: "LEGAL_INSPECTION_AUDIT", label: "Pháp chế / thanh tra / kiểm toán" },
];

export const CROSSCHECK_STATUS_LABELS: Record<CrosscheckStatus, string> = {
  Requested: "Chờ lập phiếu",
  InProgress: "Đang lập phiếu",
  Submitted: "Chờ xác nhận",
  Confirmed: "Đã xác nhận",
  Cancelled: "Đã hủy",
};

export const CROSSCHECK_STATUS_TONES: Record<CrosscheckStatus, "success" | "info" | "warning" | "danger" | "neutral"> = {
  Requested: "info",
  InProgress: "info",
  Submitted: "warning",
  Confirmed: "success",
  Cancelled: "neutral",
};

export const CROSSCHECK_RESULT_LABELS: Record<CrosscheckResult, string> = {
  Pass: "Đạt chuẩn",
  Exceed: "Vượt chuẩn",
  Defect: "Có khiếm khuyết",
};

export const CROSSCHECK_ADJUSTMENT_LABELS: Record<CrosscheckAdjustment, string> = {
  None: "Không đề xuất",
  Decrease: "Đề xuất giảm điểm",
  RecognizeExceed: "Ghi nhận vượt chuẩn",
};

export interface CrosscheckItemDto {
  id: string;
  order: number;
  taskId?: string | null;
  itemName: string;
  selfData: string;
  sourceData: string;
  result?: CrosscheckResult | null;
  resultName?: string | null;
  adjustmentType: CrosscheckAdjustment;
  adjustmentPoints?: number | null;
  adjustmentText: string;
  basisText: string;
  basisDocNumber: string;
  /** yyyy-MM-dd */
  basisDocDate?: string | null;
  basisAgency: string;
  notApplicable: boolean;
}

export interface CrosscheckHistoryDto {
  action: string;
  actionName: string;
  byUserName: string;
  at: string;
  reason?: string | null;
}

export interface CrosscheckSummaryDto {
  id: string;
  version: number;
  periodId: string;
  periodName: string;
  recordId: string;
  memberFullName: string;
  departmentName?: string | null;
  partyCellName?: string | null;
  expertDepartmentId?: string | null;
  expertUnitName: string;
  fields: string[];
  fieldNames: string[];
  status: CrosscheckStatus;
  statusName: string;
  itemCount: number;
  requestedByName: string;
  requestedAt: string;
  submittedAt?: string | null;
  confirmedAt?: string | null;
}

export interface CrosscheckDto extends CrosscheckSummaryDto {
  addresseeName: string;
  requestReason: string;
  enteredOnBehalf: boolean;
  submittedByName?: string | null;
  confirmedByName?: string | null;
  issuedDate?: string | null;
  returnReason?: string | null;
  cancelReason?: string | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  items: CrosscheckItemDto[];
  history: CrosscheckHistoryDto[];
  canEditRequest: boolean;
  canRespond: boolean;
  canSubmit: boolean;
  canConfirm: boolean;
  canReturn: boolean;
  canCancel: boolean;
}

export interface RecordCrosschecksDto {
  recordId: string;
  applicable: boolean;
  canRequest: boolean;
  blockedReason?: string | null;
  hasOpen: boolean;
  items: CrosscheckSummaryDto[];
}

export interface CrosscheckRequestItem {
  taskId?: string | null;
  itemName?: string | null;
  selfData?: string | null;
}

export interface SaveCrosscheckRequest {
  /** Bắt buộc khi sửa yêu cầu. */
  version?: number;
  expertDepartmentId?: string | null;
  expertAgencyName?: string | null;
  fields: string[];
  addresseeName: string;
  requestReason: string;
  items: CrosscheckRequestItem[];
}

export interface CrosscheckResponseItem {
  id: string;
  sourceData: string;
  result: CrosscheckResult | null;
  adjustmentType: CrosscheckAdjustment;
  adjustmentPoints: number | null;
  basisText: string;
  basisDocNumber: string;
  basisDocDate: string | null;
  basisAgency: string;
  notApplicable: boolean;
}

export interface SaveCrosscheckResponse {
  version: number;
  issuedDate: string | null;
  items: CrosscheckResponseItem[];
}

const send = <T>(endpoint: string, method: "POST" | "PUT", body: unknown): Promise<T> =>
  request<T>(endpoint, { method, body: JSON.stringify(body ?? {}) });

export const crosscheckService = {
  getByRecord(recordId: string): Promise<RecordCrosschecksDto> {
    return request<RecordCrosschecksDto>(`/evaluations/records/${recordId}/crosschecks`);
  },

  create(recordId: string, body: SaveCrosscheckRequest): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/evaluations/records/${recordId}/crosschecks`, "POST", body);
  },

  list(params: { periodId?: string; status?: string } = {}): Promise<CrosscheckSummaryDto[]> {
    const query = new URLSearchParams();
    if (params.periodId) query.set("periodId", params.periodId);
    if (params.status) query.set("status", params.status);
    const text = query.toString();
    return request<CrosscheckSummaryDto[]>(`/crosschecks${text ? `?${text}` : ""}`);
  },

  get(id: string): Promise<CrosscheckDto> {
    return request<CrosscheckDto>(`/crosschecks/${id}`);
  },

  updateRequest(id: string, body: SaveCrosscheckRequest): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}`, "PUT", body);
  },

  saveResponse(id: string, body: SaveCrosscheckResponse): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}/response`, "PUT", body);
  },

  submit(id: string, version: number): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}/submit`, "POST", { version });
  },

  confirm(id: string, version: number): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}/confirm`, "POST", { version });
  },

  returnBack(id: string, version: number, reason: string): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}/return`, "POST", { version, reason });
  },

  cancel(id: string, version: number, reason: string): Promise<CrosscheckDto> {
    return send<CrosscheckDto>(`/crosschecks/${id}/cancel`, "POST", { version, reason });
  },
};
