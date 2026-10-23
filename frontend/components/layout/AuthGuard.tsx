"use client";

import React, { useEffect, useRef } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { LayoutProvider } from "@/contexts/LayoutContext";
import { PeriodProvider } from "@/contexts/PeriodContext";
import { AppSidebar } from "@/components/layout/AppSidebar";
import { AppHeader } from "@/components/layout/AppHeader";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const isLoginPage = pathname === "/login";
  const isChangePasswordPage = pathname === "/change-password";
  // Có phiên sẵn lúc mở trang đăng nhập thì chuyển đi; đăng nhập xong thì để trang đăng nhập tự chuyển sau hiệu ứng.
  const hadSessionOnLoginEntry = useRef<boolean | null>(null);

  // Sự kiện hết hạn phiên do apiClient phát.
  useEffect(() => {
    const handleSessionExpired = () => {
      // Trang đăng nhập: chưa có phiên để kết thúc; đăng xuất/chuyển trang lúc này sẽ xóa form hoặc hủy lần đăng nhập vừa xong.
      if (isLoginPage) return;
      logout().finally(() => {
        router.replace(`/login?returnUrl=${encodeURIComponent(pathname)}`);
      });
    };

    window.addEventListener("auth:session-expired", handleSessionExpired);
    return () => {
      window.removeEventListener("auth:session-expired", handleSessionExpired);
    };
  }, [logout, pathname, router, isLoginPage]);

  useEffect(() => {
    if (!isLoginPage) hadSessionOnLoginEntry.current = null;
  }, [isLoginPage]);

  useEffect(() => {
    if (!loading) {
      if (isLoginPage && hadSessionOnLoginEntry.current === null) hadSessionOnLoginEntry.current = !!user;
      if (!user && !isLoginPage) {
        router.replace(`/login?returnUrl=${encodeURIComponent(pathname)}`);
      } else if (user?.mustChangePassword && !isChangePasswordPage) {
        router.replace("/change-password");
      } else if (user && isLoginPage && hadSessionOnLoginEntry.current) {
        router.replace("/evaluations");
      }
    }
  }, [user, loading, isLoginPage, isChangePasswordPage, pathname, router]);

  // Trang đăng nhập hiện ngay, không chờ nạp phiên.
  if (isLoginPage) {
    return <>{children}</>;
  }

  if (loading) {
    return (
      <div className="d-flex flex-column align-items-center justify-content-center min-vh-100 bg-light loading-delay">
        <div className="spinner-border text-primary mb-3" role="status" style={{ width: "2.2rem", height: "2.2rem" }}>
          <span className="visually-hidden">Đang tải...</span>
        </div>
        <div className="fw-semibold text-secondary small">
          Đang khởi tạo phiên làm việc...
        </div>
      </div>
    );
  }

  if (!user) {
    return null;
  }

  // Phải đổi mật khẩu: không render trang nghiệp vụ (tránh 403) trong lúc chờ chuyển trang.
  if (user.mustChangePassword && !isChangePasswordPage) {
    return null;
  }

  // Kỳ chung chỉ nạp khi không phải đổi mật khẩu (tài khoản đó chỉ được gọi API đổi mật khẩu).
  return (
    <LayoutProvider>
      <PeriodProvider enabled={!user.mustChangePassword && !isChangePasswordPage}>
        <div className="app-shell">
          <AppSidebar />
          <div className="app-content">
            <AppHeader />
            <main className="app-main">
              <div className="app-main-inner">{children}</div>
            </main>
          </div>
        </div>
      </PeriodProvider>
    </LayoutProvider>
  );
}

export default AuthGuard;
