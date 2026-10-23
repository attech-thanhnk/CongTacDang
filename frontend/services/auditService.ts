import { request } from "./apiClient";
import type { PagedResult } from "./userService";

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

export interface AuditLogFilters {
  entityType?: string;
  entityId?: string;
  action?: string;
  page?: number;
  pageSize?: number;
}

/** Số đếm tính trên toàn bộ bản ghi khớp bộ lọc. */
export interface AuditLogPage extends PagedResult<AuditLogDto> {
  actionCounts: Record<string, number>;
  actorCount: number;
  entityTypes: string[];
}

export type LoginResult = "Success" | "InvalidPassword" | "UnknownUser" | "LockedOut" | "Disabled";

export const LOGIN_RESULT_LABELS: Record<LoginResult, string> = {
  Success: "Thành công",
  InvalidPassword: "Sai mật khẩu",
  UnknownUser: "Tên đăng nhập không tồn tại",
  LockedOut: "Đang bị khóa tạm",
  Disabled: "Tài khoản bị vô hiệu",
};

export interface LoginEventDto {
  id: string;
  userId?: string | null;
  usernameAttempted: string;
  result: LoginResult;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
}

/** Thời gian theo ISO, UTC. */
export interface LoginEventFilters {
  userId?: string;
  from?: string;
  to?: string;
  result?: LoginResult;
  page?: number;
  pageSize?: number;
}

export const auditService = {
  /** Mới nhất trước, phân trang phía máy chủ. */
  async getLogs(filters: AuditLogFilters = {}): Promise<AuditLogPage> {
    const params = new URLSearchParams();
    if (filters.entityType) params.set("entityType", filters.entityType);
    if (filters.entityId) params.set("entityId", filters.entityId);
    if (filters.action) params.set("action", filters.action);
    params.set("page", String(filters.page || 1));
    params.set("pageSize", String(filters.pageSize || 20));
    return request<AuditLogPage>(`/admin/audit?${params.toString()}`);
  },

  /** Mới nhất trước, phân trang phía máy chủ. */
  async getLoginEvents(filters: LoginEventFilters = {}): Promise<PagedResult<LoginEventDto>> {
    const params = new URLSearchParams();
    if (filters.userId) params.set("userId", filters.userId);
    if (filters.from) params.set("from", filters.from);
    if (filters.to) params.set("to", filters.to);
    if (filters.result) params.set("result", filters.result);
    params.set("page", String(filters.page || 1));
    params.set("pageSize", String(filters.pageSize || 50));
    return request<PagedResult<LoginEventDto>>(`/audit/logins?${params.toString()}`);
  },
};
