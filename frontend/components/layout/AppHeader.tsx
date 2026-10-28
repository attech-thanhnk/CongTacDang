"use client";

import React, { useEffect } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { usePageTitle } from "@/contexts/PageTitleContext";
import { AccountMenu } from "./AccountMenu";
import { NotificationBell } from "./NotificationBell";
import { PeriodPicker } from "./PeriodPicker";
import { recordPath } from "@/lib/navHistory";
import { buildMenu, findMenuTrail } from "./menu";

/** Thanh trên: ☰, tiêu đề trang; giữa là chọn kỳ; phải là hướng dẫn, chuông, tài khoản. */
export function AppHeader() {
  const { user, hasPermission } = useAuth();
  const { toggleSidebar } = useLayout();
  const pageTitle = usePageTitle();
  const pathname = usePathname();
  const trail = user ? findMenuTrail(buildMenu(user, hasPermission), pathname) : null;

  useEffect(() => {
    recordPath(pathname);
  }, [pathname]);

  return (
    <header className="app-header">
      <div className="hdr-left">
        <button type="button" onClick={toggleSidebar} className="app-header-menu" aria-label="Mở menu">
          <i className="bi bi-list" />
        </button>
        {/* Trang đã đăng ký tiêu đề thì đó là h1 duy nhất; trang khác dùng tên mục menu làm dự phòng. */}
        {pageTitle ? (
          <h1 className="hdr-title">{pageTitle}</h1>
        ) : (
          trail && <div className="hdr-title">{trail.item.title}</div>
        )}
      </div>
      <PeriodPicker />
      <div className="app-header-right">
        {user && !user.mustChangePassword && (
          <Link href="/help" className="app-header-help" aria-label="Hướng dẫn" title="Hướng dẫn">
            <i className="bi bi-question-circle" />
          </Link>
        )}
        {/* Tài khoản phải đổi mật khẩu chỉ được gọi API đổi mật khẩu — không nạp chuông nhắc việc (tránh 403). */}
        {user && !user.mustChangePassword && <NotificationBell />}
        <AccountMenu />
      </div>
    </header>
  );
}

export default AppHeader;
