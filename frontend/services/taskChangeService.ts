import { request } from "./apiClient";

/** Loại đề nghị: điều chỉnh / chuyển tiếp / loại trừ (Mẫu 04), việc đột xuất (Mẫu 05). */
export type TaskChangeType = "Adjust" | "Carryover" | "Exclude" | "Urgent";
export type TaskChangeStatus = "Pending" | "Approved" | "Rejected";

export const TASK_CHANGE_TYPE_LABELS: Record<TaskChangeType, string> = {
  Adjust: "Điều chỉnh việc",
  Carryover: "Chuyển tiếp sang quý sau",
  Exclude: "Loại trừ việc",
  Urgent: "Việc đột xuất",
};

/** Điều chỉnh: trường để trống là giữ nguyên; việc đột xuất: toàn bộ việc mới. */
export interface TaskChangeProposal {
  taskName?: string | null;
  targetOutput?: string | null;
  deadline?: string | null;
  axisCode?: string | null;
  productCode?: string | null;
  role?: string | null;
  exceedCriteria?: string | null;
  evidenceSource?: string | null;
  confirmer?: string | null;
  weight?: number | null;
}

export interface TaskChangeWeightDto {
  taskId?: string | null;
  taskName: string;
  before?: number | null;
  after: number;
}

export interface TaskChangeRequestDto {
  id: string;
  version: number;
  recordId: string;
  type: TaskChangeType;
  typeName: string;
  taskId?: string | null;
  taskName?: string | null;
  proposal: TaskChangeProposal;
  weights: TaskChangeWeightDto[];
  reason: string;
  status: TaskChangeStatus;
  statusName: string;
  requestedByName: string;
  requestedAt: string;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionComment?: string | null;
  /** Người xem duyệt / không duyệt được (máy chủ tính). */
  canDecide: boolean;
}

export interface RecordTaskChangesDto {
  recordId: string;
  applicable: boolean;
  canRequestChange: boolean;
  canRecordUrgent: boolean;
  blockedReason?: string | null;
  hasPending: boolean;
  requests: TaskChangeRequestDto[];
}

export interface CreateTaskChangeRequest {
  /** Phiên bản hồ sơ đã đọc. */
  version: number;
  type: TaskChangeType;
  taskId?: string | null;
  proposal?: TaskChangeProposal;
  /** id việc → trọng số sau điều chỉnh. */
  weights: Record<string, number>;
  reason: string;
}

const base = (recordId: string) => `/evaluations/records/${recordId}/task-changes`;

const post = <T>(endpoint: string, body: unknown): Promise<T> =>
  request<T>(endpoint, { method: "POST", body: JSON.stringify(body ?? {}) });

export const taskChangeService = {
  get(recordId: string): Promise<RecordTaskChangesDto> {
    return request<RecordTaskChangesDto>(base(recordId));
  },

  create(recordId: string, body: CreateTaskChangeRequest): Promise<RecordTaskChangesDto> {
    return post<RecordTaskChangesDto>(base(recordId), body);
  },

  approve(recordId: string, id: string, version: number, comment?: string): Promise<RecordTaskChangesDto> {
    return post<RecordTaskChangesDto>(`${base(recordId)}/${id}/approve`, { version, comment });
  },

  reject(recordId: string, id: string, version: number, comment: string): Promise<RecordTaskChangesDto> {
    return post<RecordTaskChangesDto>(`${base(recordId)}/${id}/reject`, { version, comment });
  },
};
