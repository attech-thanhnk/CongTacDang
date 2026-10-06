"use client";

import React, { useEffect } from "react";
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

  // Lắng nghe sự kiện hết hạn phiên làm việc từ apiClient để điều hướng mượt mà
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
    if (!loading) {
      if (!user && !isLoginPage) {
        router.replace(`/login?returnUrl=${encodeURIComponent(pathname)}`);
      } else if (user?.mustChangePassword && !isChangePasswordPage) {
        router.replace("/change-password");
      } else if (user && isLoginPage) {
        router.replace("/evaluations");
      }
    }
  }, [user, loading, isLoginPage, isChangePasswordPage, pathname, router]);

  // Nếu là trang đăng nhập: Render trực tiếp ngay lập tức, không để spinner che khuất
  if (isLoginPage) {
    return <>{children}</>;
  }

  // Nếu đang nạp phiên ở các trang nội bộ
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

  // Nếu chưa đăng nhập ở trang nội bộ: Không render layout làm việc
  if (!user) {
    return null;
  }

  // Phải đổi mật khẩu: không render trang nghiệp vụ (tránh gọi API bị 403) trong lúc chờ chuyển sang /change-password
  if (user.mustChangePassword && !isChangePasswordPage) {
    return null;
  }

  // Đã xác thực: khung làm việc (thanh bên, thanh trên mảnh, vùng nội dung căn giữa).
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
