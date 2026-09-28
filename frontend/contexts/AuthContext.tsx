"use client";

import React, { createContext, useContext, useState, useEffect } from "react";
import { authService, UserSession } from "@/services/authService";

interface AuthContextType {
  user: UserSession | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<UserSession>;
  logout: () => Promise<void>;
  hasPermission: (permissionCode: string) => boolean;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserSession | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = async () => {
    try {
      const data = await authService.getMe();
      setUser(data);
    } catch {
      setUser(null);
    }
  };

  useEffect(() => {
    // Chỉ kiểm tra phiên đăng nhập thực tế từ máy chủ qua HttpOnly Cookie
    authService
      .getMe()
      .then((data) => {
        setUser(data);
      })
      .catch(() => {
        setUser(null);
      })
      .finally(() => setLoading(false));
  }, []);

  const login = async (username: string, password: string): Promise<UserSession> => {
    const session = await authService.login(username, password);
    setUser(session);
    return session;
  };

  const logout = async (): Promise<void> => {
    await authService.logout();
    setUser(null);
  };

  // Task 09: chỉ kiểm tra mã quyền (danh mục PermissionCodes) — không có ngoại lệ theo tên vai trò.
  // Phạm vi (Phòng/Chi bộ) được máy chủ kiểm tra; ở đây chỉ để ẩn/hiện menu, nút.
  const hasPermission = (permissionCode: string): boolean => {
    if (!user || !user.permissions) return false;
    return user.permissions.includes(permissionCode);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        loading,
        login,
        logout,
        hasPermission,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
