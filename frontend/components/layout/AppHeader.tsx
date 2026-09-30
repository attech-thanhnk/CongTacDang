"use client";

import React from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { NotificationBell } from "./NotificationBell";

/** Thanh trên cùng mảnh: nút mở menu (màn hẹp) và chuông nhắc việc. */
export function AppHeader() {
  const { user } = useAuth();
  const { toggleSidebar } = useLayout();

  return (
    <header className="app-header">
      <button type="button" onClick={toggleSidebar} className="app-header-menu d-md-none" aria-label="Mở menu">
        <i className="bi bi-list" />
      </button>
      <div className="app-header-right">
        {/* Tài khoản phải đổi mật khẩu chỉ được gọi API đổi mật khẩu — không nạp chuông nhắc việc (tránh 403). */}
        {user && !user.mustChangePassword && <NotificationBell />}
      </div>
    </header>
  );
}

export default AppHeader;
