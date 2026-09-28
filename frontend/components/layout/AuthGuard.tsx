"use client";

import React, { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { LayoutProvider } from "@/contexts/LayoutContext";
import { AppSidebar } from "@/components/layout/AppSidebar";
import { AppHeader } from "@/components/layout/AppHeader";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { user, loading, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const isLoginPage = pathname === "/login";

  // Lắng nghe sự kiện hết hạn phiên làm việc từ apiClient để điều hướng mượt mà
  useEffect(() => {
    const handleSessionExpired = () => {
      logout().finally(() => {
        router.replace(`/login?returnUrl=${encodeURIComponent(pathname)}`);
      });
    };

    window.addEventListener("auth:session-expired", handleSessionExpired);
    return () => {
      window.removeEventListener("auth:session-expired", handleSessionExpired);
    };
  }, [logout, pathname, router]);

  useEffect(() => {
    if (!loading) {
      if (!user && !isLoginPage) {
        router.replace(`/login?returnUrl=${encodeURIComponent(pathname)}`);
      } else if (user && isLoginPage) {
        router.replace("/evaluations");
      }
    }
  }, [user, loading, isLoginPage, pathname, router]);

  // Nếu là trang đăng nhập: Render trực tiếp ngay lập tức, không để spinner che khuất
  if (isLoginPage) {
    return <>{children}</>;
  }

  // Nếu đang nạp phiên ở các trang nội bộ
  if (loading) {
    return (
      <div className="d-flex flex-column align-items-center justify-content-center min-vh-100 bg-light">
        <div className="spinner-border text-primary mb-3" role="status" style={{ width: "2.2rem", height: "2.2rem" }}>
          <span className="visually-hidden">Đang tải...</span>
        </div>
        <div className="fw-semibold text-secondary small">
          Đang khởi tạo phiên làm việc...
        </div>
      </div>
    );
  }

  // Nếu chưa đăng nhập ở trang nội bộ: Không render layout làm việc
  if (!user) {
    return null;
  }

  // Đã xác thực thành công: Render toàn bộ hệ thống làm việc trong LayoutProvider
  return (
    <LayoutProvider>
      <div className="app-shell">
        {/* Sidebar */}
        <AppSidebar />

        {/* Content area */}
        <div className="app-content">
          <AppHeader />
          <main className="app-main">
            {children}
          </main>
          <footer
            style={{
              padding: "8px 20px",
              background: "var(--bg-card)",
              borderTop: "1px solid var(--border-base)",
              textAlign: "center",
              fontSize: "11px",
              color: "var(--text-muted)",
            }}
          >
            © 2026 Đảng bộ ATTECH — Hệ thống quản trị đánh giá cán bộ định kỳ
          </footer>
        </div>
      </div>
    </LayoutProvider>
  );
}

export default AuthGuard;
