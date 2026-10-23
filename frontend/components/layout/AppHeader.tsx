"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { NotificationBell } from "./NotificationBell";
import { buildMenu, findMenuTrail } from "./menu";

/** Thanh trên cùng: nút mở menu (màn hẹp), đường dẫn trang hiện tại và chuông nhắc việc. */
export function AppHeader() {
  const { user, hasPermission } = useAuth();
  const { toggleSidebar } = useLayout();
  const pathname = usePathname();
  const trail = user ? findMenuTrail(buildMenu(user, hasPermission), pathname) : null;

  return (
    <header className="app-header">
      <button type="button" onClick={toggleSidebar} className="app-header-menu d-md-none" aria-label="Mở menu">
        <i className="bi bi-list" />
      </button>
      {trail && (
        <nav className="app-crumbs" aria-label="Đường dẫn">
          {trail.group && (
            <>
              <span className="app-crumb-group">{trail.group.title}</span>
              <i className="bi bi-chevron-right app-crumb-sep app-crumb-group" aria-hidden="true" />
            </>
          )}
          {trail.isDetail ? (
            <>
              <Link href={trail.item.href}>{trail.item.title}</Link>
              <i className="bi bi-chevron-right app-crumb-sep" aria-hidden="true" />
              <span aria-current="page">Chi tiết</span>
            </>
          ) : (
            <span aria-current="page">{trail.item.title}</span>
          )}
        </nav>
      )}
      <div className="app-header-right">
        {/* Tài khoản phải đổi mật khẩu chỉ được gọi API đổi mật khẩu — không nạp chuông nhắc việc (tránh 403). */}
        {user && !user.mustChangePassword && <NotificationBell />}
      </div>
    </header>
  );
}

export default AppHeader;
