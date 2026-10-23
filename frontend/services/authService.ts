import { useEffect, useState } from "react";
import { request } from "./apiClient";

/** Nơi người dùng làm được một quyền; danh sách đơn vị đã gồm cấp dưới. */
export interface PermissionScope {
  isGlobal: boolean;
  departmentIds: string[];
  partyCellIds: string[];
}

export interface AccessGrant {
  code: string;
  scopeType: "Global" | "Department" | "PartyCell";
  /** null khi Toàn công ty */
  scopeId: string | null;
  scopeName: string;
}

export interface UserSession {
  id: string;
  fullName: string;
  userName: string;
  /** Chỉ để hiển thị, không dùng để phân quyền */
  roles: string[];
  /** Mã quyền có ở ít nhất một phạm vi */
  permissions: string[];
  /** Phạm vi đơn vị gồm cả cấp dưới */
  grants: AccessGrant[];
  mustChangePassword: boolean;
  expiresAt?: string;
}

export const authService = {
  /** Máy chủ đặt cookie HttpOnly auth_token */
  async login(username: string, password: string): Promise<UserSession> {
    return request<UserSession>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });
  },

  async logout(): Promise<void> {
    return request<void>("/auth/logout", {
      method: "POST",
    });
  },

  async getMe(): Promise<UserSession> {
    return request<UserSession>("/auth/me");
  },

  async getScope(permission: string): Promise<PermissionScope> {
    return request<PermissionScope>(`/auth/scope?permission=${encodeURIComponent(permission)}`);
  },

  async refreshToken(): Promise<UserSession> {
    return request<UserSession>("/auth/refresh-token", {
      method: "POST",
    });
  },

  /** Máy chủ cấp lại cookie cho phiên này và đăng xuất mọi phiên khác. */
  async changePassword(currentPassword: string, newPassword: string): Promise<UserSession> {
    return request<UserSession>("/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword }),
    });
  },
};

/** null khi đang tải. Dùng để chỉ cho chọn đúng nơi được làm thay vì chọn rồi mới bị máy chủ từ chối. */
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

/** Toàn công ty → giữ nguyên. */
export function withinScope<T extends { id: string }>(items: T[], scope: PermissionScope | null, kind: "department" | "partyCell"): T[] {
  if (!scope) return [];
  if (scope.isGlobal) return items;
  const allowed = new Set(kind === "department" ? scope.departmentIds : scope.partyCellIds);
  return items.filter((item) => allowed.has(item.id));
}
