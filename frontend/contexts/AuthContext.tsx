"use client";

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { authService, UserSession } from "@/services/authService";

/** Loại phạm vi của một quyền được cấp. */
export type GrantScopeType = "Global" | "Department" | "PartyCell";

/** Một quyền kèm phạm vi, lấy từ trường `grants` của `/api/auth/me` (và phản hồi login/refresh). */
export interface PermissionGrant {
  code: string;
  scopeType: GrantScopeType;
  scopeId: string | null;
  scopeName: string;
}

/** Phiên đăng nhập kèm danh sách quyền theo phạm vi. `grants` = null khi máy chủ chưa trả trường này. */
export type AuthUser = Omit<UserSession, "roles"> & {
  /** Tên vai trò từ các bản gán đang hiệu lực (chỉ để hiển thị, không dùng để phân quyền). */
  roles: string[];
  grants: PermissionGrant[] | null;
};

interface AuthContextType {
  user: AuthUser | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<AuthUser>;
  logout: () => Promise<void>;
  /** Có mã quyền ở bất kỳ phạm vi nào (dùng để ẩn/hiện menu, nút). Máy chủ luôn kiểm tra lại. */
  hasPermission: (permissionCode: string) => boolean;
  /**
   * Có mã quyền trong phạm vi chỉ định. Bản cấp phạm vi Toàn công ty bao trùm mọi phạm vi.
   * Không truyền `scopeId` → có ở bất kỳ đối tượng nào thuộc loại phạm vi đó.
   * Máy chủ chưa trả `grants` → chỉ dựa vào `permissions` (coi như không có thông tin phạm vi).
   */
  hasPermissionIn: (permissionCode: string, scopeType: GrantScopeType, scopeId?: string | null) => boolean;
  refreshUser: () => Promise<void>;
}

const SCOPE_BY_NUMBER: Record<number, GrantScopeType> = { 0: "Global", 1: "Department", 2: "PartyCell" };

/** Chuẩn hóa `scopeType` — máy chủ có thể serialize enum dạng số (0/1/2) hoặc chuỗi. */
function normalizeScopeType(value: unknown): GrantScopeType | null {
  if (typeof value === "number") return SCOPE_BY_NUMBER[value] ?? null;
  if (typeof value === "string") {
    const trimmed = value.trim();
    if (/^\d+$/.test(trimmed)) return SCOPE_BY_NUMBER[Number(trimmed)] ?? null;
    const lower = trimmed.toLowerCase();
    if (lower === "global") return "Global";
    if (lower === "department") return "Department";
    if (lower === "partycell") return "PartyCell";
  }
  return null;
}

function normalizeGrants(raw: unknown): PermissionGrant[] | null {
  if (!Array.isArray(raw)) return null;
  const grants: PermissionGrant[] = [];
  for (const item of raw) {
    if (!item || typeof item !== "object") continue;
    const record = item as Record<string, unknown>;
    const scopeType = normalizeScopeType(record.scopeType);
    if (typeof record.code !== "string" || !scopeType) continue;
    grants.push({
      code: record.code,
      scopeType,
      scopeId: typeof record.scopeId === "string" && record.scopeId ? record.scopeId : null,
      scopeName: typeof record.scopeName === "string" ? record.scopeName : "",
    });
  }
  return grants;
}

/** Nhận phản hồi `/auth/me` | login | refresh, bổ sung `grants` (có thể chưa có). */
function toAuthUser(session: UserSession): AuthUser {
  const raw = session as UserSession & { grants?: unknown };
  return {
    ...session,
    roles: Array.isArray(session.roles) ? session.roles.filter((r): r is string => typeof r === "string") : [],
    permissions: Array.isArray(session.permissions) ? session.permissions : [],
    grants: normalizeGrants(raw.grants),
  };
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    try {
      setUser(toAuthUser(await authService.getMe()));
    } catch {
      setUser(null);
    }
  }, []);

  useEffect(() => {
    // Chỉ kiểm tra phiên đăng nhập thực tế từ máy chủ qua HttpOnly Cookie
    authService
      .getMe()
      .then((data) => setUser(toAuthUser(data)))
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<AuthUser> => {
    const session = toAuthUser(await authService.login(username, password));
    setUser(session);
    return session;
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    await authService.logout();
    setUser(null);
  }, []);

  // Chỉ kiểm tra mã quyền (danh mục PermissionCodes) — không có ngoại lệ theo tên vai trò.
  // Phạm vi (Phòng/Chi bộ) được máy chủ kiểm tra; ở đây chỉ để ẩn/hiện menu, nút.
  const hasPermission = useCallback(
    (permissionCode: string): boolean => !!user && user.permissions.includes(permissionCode),
    [user]
  );

  const hasPermissionIn = useCallback(
    (permissionCode: string, scopeType: GrantScopeType, scopeId?: string | null): boolean => {
      if (!user || !user.permissions.includes(permissionCode)) return false;
      if (user.grants === null) return true;
      return user.grants.some((grant) => {
        if (grant.code !== permissionCode) return false;
        if (grant.scopeType === "Global") return true;
        if (grant.scopeType !== scopeType) return false;
        return !scopeId || grant.scopeId === scopeId;
      });
    },
    [user]
  );

  const value = useMemo(
    () => ({ user, loading, login, logout, hasPermission, hasPermissionIn, refreshUser }),
    [user, loading, login, logout, hasPermission, hasPermissionIn, refreshUser]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}

/**
 * Nhãn hiển thị dưới tên người dùng (thanh bên, đầu trang): tên vai trò đầu tiên từ bản gán.
 * Bỏ qua giá trị dạng mã (chữ hoa + gạch dưới) do máy chủ cũ trả về.
 */
export function sessionRoleLabel(user: AuthUser | null): string {
  const name = user?.roles.find((role) => role.trim() && !/^[A-Z0-9_]+$/.test(role));
  return name || "Cán bộ";
}
