import { request } from "./apiClient";
import type { PagedResult } from "./userService";

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

/** Bộ lọc tra cứu audit log. */
export interface AuditLogFilters {
  entityType?: string;
  entityId?: string;
  limit?: number;
}

/** Kết quả một lần đăng nhập. */
export type LoginResult = "Success" | "InvalidPassword" | "UnknownUser" | "LockedOut" | "Disabled";

export const LOGIN_RESULT_LABELS: Record<LoginResult, string> = {
  Success: "Thành công",
  InvalidPassword: "Sai mật khẩu",
  UnknownUser: "Tên đăng nhập không tồn tại",
  LockedOut: "Đang bị khóa tạm",
  Disabled: "Tài khoản bị vô hiệu",
};

/** Một bản ghi nhật ký đăng nhập. */
export interface LoginEventDto {
  id: string;
  userId?: string | null;
  usernameAttempted: string;
  result: LoginResult;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
}

/** Bộ lọc nhật ký đăng nhập (thời gian theo ISO, UTC). */
export interface LoginEventFilters {
  userId?: string;
  from?: string;
  to?: string;
  result?: LoginResult;
  page?: number;
  pageSize?: number;
}

/** Dịch vụ truy vấn audit log, nhật ký đăng nhập và lịch sử quy trình đánh giá. */
export const auditService = {
  /** Lấy các thao tác gần nhất theo loại hoặc Id entity. */
  async getLogs(filters: AuditLogFilters = {}): Promise<AuditLogDto[]> {
    const params = new URLSearchParams();
    if (filters.entityType) params.set("entityType", filters.entityType);
    if (filters.entityId) params.set("entityId", filters.entityId);
    params.set("limit", String(filters.limit || 100));
    return request<AuditLogDto[]>(`/admin/audit?${params.toString()}`);
  },

  /** Nhật ký đăng nhập, mới nhất trước, phân trang phía máy chủ. */
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
