"use client";

import React, { useEffect, useRef } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { usePageTitle } from "@/contexts/PageTitleContext";
import { AccountMenu } from "./AccountMenu";
import { NotificationBell } from "./NotificationBell";
import { PeriodPicker } from "./PeriodPicker";
import { buildMenu, findMenuTrail } from "./menu";

/** Thanh trên: ☰, nút quay lại (trang chi tiết), tiêu đề trang; giữa là chọn kỳ; phải là hướng dẫn, chuông, tài khoản. */
export function AppHeader() {
  const { user, hasPermission } = useAuth();
  const { toggleSidebar } = useLayout();
  const pageTitle = usePageTitle();
  const pathname = usePathname();
  const router = useRouter();
  const trail = user ? findMenuTrail(buildMenu(user, hasPermission), pathname) : null;

  // Trang ngay trước đó: nếu là danh sách cha thì quay lại bằng lịch sử để giữ bộ lọc và trang đang xem.
  const current = useRef<string | null>(null);
  const previous = useRef<string | null>(null);
  useEffect(() => {
    if (current.current !== pathname) {
      previous.current = current.current;
      current.current = pathname;
    }
  }, [pathname]);

  const goBack = () => {
    if (!trail) return;
    if (previous.current === trail.item.href) {
      router.back();
    } else {
      router.push(trail.item.href);
    }
  };

  return (
    <header className="app-header">
      <div className="hdr-left">
        <button type="button" onClick={toggleSidebar} className="app-header-menu" aria-label="Mở menu">
          <i className="bi bi-list" />
        </button>
        {trail?.isDetail && (
          <button type="button" className="hdr-back" onClick={goBack} aria-label={`Quay lại ${trail.item.title}`}>
            <i className="bi bi-arrow-left" aria-hidden="true" />
            <span>{trail.item.title}</span>
          </button>
        )}
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
