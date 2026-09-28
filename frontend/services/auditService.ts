import { request } from "./apiClient";

/** Một bản ghi thao tác trong audit log tập trung. */
export interface AuditLogDto {
  id: string;
  actorId?: string | null;
  actorName: string;
  action: string;
  entityType: string;
  entityId: string;
  oldValues: string;
  newValues: string;
  ipAddress?: string | null;
  requestPath?: string | null;
  createdAt: string;
}

/** Một mốc chuyển trạng thái của hồ sơ đánh giá. */
export interface EvaluationRecordHistoryDto {
  id: string;
  recordId: string;
  fromStatus?: string | null;
  toStatus: string;
  actorId?: string | null;
  actorName: string;
  comment?: string | null;
  createdAt: string;
}

/** Bộ lọc tra cứu audit log. */
export interface AuditLogFilters {
  entityType?: string;
  entityId?: string;
  limit?: number;
}

/** Dịch vụ truy vấn audit log và lịch sử quy trình đánh giá. */
export const auditService = {
  /** Lấy các thao tác gần nhất theo loại hoặc Id entity. */
  async getLogs(filters: AuditLogFilters = {}): Promise<AuditLogDto[]> {
    const params = new URLSearchParams();
    if (filters.entityType) params.set("entityType", filters.entityType);
    if (filters.entityId) params.set("entityId", filters.entityId);
    params.set("limit", String(filters.limit || 100));

    const query = params.toString();
    return request<AuditLogDto[]>(`/admin/audit${query ? `?${query}` : ""}`);
  },

  /** Lấy lịch sử chuyển trạng thái của một hồ sơ đánh giá. */
  async getRecordHistory(recordId: string): Promise<EvaluationRecordHistoryDto[]> {
    return request<EvaluationRecordHistoryDto[]>(`/evaluations/records/${recordId}/history`);
  },
};
