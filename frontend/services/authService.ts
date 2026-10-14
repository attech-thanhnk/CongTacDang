import { useEffect, useState } from "react";
import { request } from "./apiClient";

/** Nơi người dùng hiện tại làm được một quyền (toàn công ty, hoặc các đơn vị / tổ chức Đảng — đã gồm cấp dưới). */
export interface PermissionScope {
  isGlobal: boolean;
  departmentIds: string[];
  partyCellIds: string[];
}

/** Một quyền kèm phạm vi áp dụng */
export interface AccessGrant {
  /** Mã quyền */
  code: string;
  /** Loại phạm vi */
  scopeType: "Global" | "Department" | "PartyCell";
  /** Id đơn vị chính quyền / tổ chức Đảng được gán (null khi Toàn công ty) */
  scopeId: string | null;
  /** Tên phạm vi hiển thị */
  scopeName: string;
}

/** Thông tin phiên làm việc của người dùng đang đăng nhập */
export interface UserSession {
  /** Mã định danh cán bộ */
  id: string;
  /** Họ và tên cán bộ */
  fullName: string;
  /** Tên tài khoản */
  userName: string;
  /** Tên các vai trò đang hiệu lực — chỉ để hiển thị, không dùng để phân quyền */
  roles: string[];
  /** Các mã quyền có ở ít nhất một phạm vi (system.users.read, evaluation.read, ...) */
  permissions: string[];
  /** Quyền kèm phạm vi (Toàn công ty / Đơn vị chính quyền / Tổ chức Đảng — gồm cả đơn vị cấp dưới) */
  grants: AccessGrant[];
  /** Bắt buộc đổi mật khẩu tạm */
  mustChangePassword: boolean;
  /** Thời điểm hết hạn phiên làm việc */
  expiresAt?: string;
}

export const authService = {
  /** Đăng nhập hệ thống — nhận session và cookie HttpOnly auth_token */
  async login(username: string, password: string): Promise<UserSession> {
    return request<UserSession>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });
  },

  /** Đăng xuất hệ thống — xóa cookie trên trình duyệt */
  async logout(): Promise<void> {
    return request<void>("/auth/logout", {
      method: "POST",
    });
  },

  /** Lấy thông tin phiên đăng nhập hiện tại từ JWT claims */
  async getMe(): Promise<UserSession> {
    return request<UserSession>("/auth/me");
  },

  /** Nơi người dùng hiện tại làm được quyền <paramref name="permission"/>. */
  async getScope(permission: string): Promise<PermissionScope> {
    return request<PermissionScope>(`/auth/scope?permission=${encodeURIComponent(permission)}`);
  },

  /** Làm mới phiên làm việc bằng Refresh Token */
  async refreshToken(): Promise<UserSession> {
    return request<UserSession>("/auth/refresh-token", {
      method: "POST",
    });
  },

  /**
   * Đổi mật khẩu của tài khoản hiện tại. Máy chủ cấp lại cookie cho phiên này và đăng xuất mọi phiên khác;
   * trả về thông tin phiên mới (mustChangePassword = false).
   */
  async changePassword(currentPassword: string, newPassword: string): Promise<UserSession> {
    return request<UserSession>("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    });
  },
};

/**
 * Phạm vi của một quyền cho người dùng hiện tại (null khi đang tải). Dùng để chỉ cho chọn đúng nơi được làm
 * thay vì để chọn rồi mới bị máy chủ từ chối.
 */
export function usePermissionScope(permission: string): PermissionScope | null {
  const [scope, setScope] = useState<PermissionScope | null>(null);
  useEffect(() => {
    let active = true;
    authService
      .getScope(permission)
      .then((value) => active && setScope(value))
      .catch(() => active && setScope({ isGlobal: false, departmentIds: [], partyCellIds: [] }));
    return () => {
      active = false;
    };
  }, [permission]);
  return scope;
}

/** Lọc danh sách đơn vị theo phạm vi (toàn công ty → giữ nguyên). */
export function withinScope<T extends { id: string }>(items: T[], scope: PermissionScope | null, kind: "department" | "partyCell"): T[] {
  if (!scope) return [];
  if (scope.isGlobal) return items;
  const allowed = new Set(kind === "department" ? scope.departmentIds : scope.partyCellIds);
  return items.filter((item) => allowed.has(item.id));
}
