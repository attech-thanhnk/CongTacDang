"use client";

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { AccessGrant, authService, UserSession } from "@/services/authService";

/** Loại phạm vi của một quyền được cấp. */
export type GrantScopeType = AccessGrant["scopeType"];

/** Phiên đăng nhập: mã quyền (`permissions`) và quyền kèm phạm vi (`grants`) do máy chủ tính từ bản gán vai trò. */
export type AuthUser = UserSession;

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
   */
  hasPermissionIn: (permissionCode: string, scopeType: GrantScopeType, scopeId?: string | null) => boolean;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    try {
      setUser(await authService.getMe());
    } catch {
      setUser(null);
    }
  }, []);

  useEffect(() => {
    // Chỉ kiểm tra phiên đăng nhập thực tế từ máy chủ qua HttpOnly Cookie
    authService
      .getMe()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<AuthUser> => {
    const session = await authService.login(username, password);
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
    (permissionCode: string, scopeType: GrantScopeType, scopeId?: string | null): boolean =>
      !!user &&
      user.grants.some((grant) => {
        if (grant.code !== permissionCode) return false;
        if (grant.scopeType === "Global") return true;
        if (grant.scopeType !== scopeType) return false;
        return !scopeId || grant.scopeId === scopeId;
      }),
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

/** Nhãn hiển thị dưới tên người dùng (thanh bên, đầu trang): tên vai trò đầu tiên từ bản gán đang hiệu lực. */
export function sessionRoleLabel(user: AuthUser | null): string {
  return user?.roles.find((role) => role.trim()) || "Cán bộ";
}
