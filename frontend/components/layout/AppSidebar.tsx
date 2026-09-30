"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { useOrganizationInfo } from "@/services/organizationSettingsService";
import { AccountMenu } from "./AccountMenu";

interface MenuItem {
  title: string;
  href: string;
  icon: string;
  /** Mã quyền cần có (ở bất kỳ phạm vi nào). */
  permission?: string;
  /** Điều kiện tùy chỉnh (ưu tiên hơn `permission`). */
  check?: () => boolean;
}

interface MenuGroup {
  title?: string;
  items: MenuItem[];
}

/** Chữ cái đầu của hai từ cuối trong họ tên. */
/** Biểu tượng Đảng bộ: ngôi sao trong vòng tròn đỏ son. */
function Emblem() {
  return (
    <div className="side-emblem" aria-hidden="true">
      <svg viewBox="0 0 24 24">
        <path d="M12 2.5l2.7 6.1 6.6.5-5 4.4 1.5 6.5L12 16.6 6.2 20l1.5-6.5-5-4.4 6.6-.5z" />
      </svg>
    </div>
  );
}

export function AppSidebar() {
  const pathname = usePathname();
  const { user, hasPermission } = useAuth();
  const { isSidebarOpen, closeSidebar } = useLayout();
  const { periods, periodId, setPeriodId } = usePeriod();
  const org = useOrganizationInfo();

  // Tên hiển thị lấy từ cài đặt đơn vị: dòng lớn là tên viết tắt, dòng nhỏ là phần còn lại của tên hệ thống.
  const brandMain = org?.shortName || org?.systemName || "";
  const brandSub = org
    ? (org.shortName && org.systemName.includes(org.shortName)
        ? org.systemName.replace(org.shortName, "").trim()
        : org.systemName !== brandMain
          ? org.systemName
          : "")
    : "";

  const showPeriodPicker = !!user && !user.mustChangePassword && pathname !== "/change-password";

  // Menu chỉ hiện theo mã quyền — máy chủ luôn kiểm tra lại, kể cả phạm vi.
  const hasAnyEvaluationAction = () =>
    (user?.permissions ?? []).some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");

  const menuGroups: MenuGroup[] = [
    {
      items: [
        { title: "Tổng quan", href: "/", icon: "bi-house" },
        { title: "Việc cần xử lý", href: "/work-queue", icon: "bi-inbox", check: hasAnyEvaluationAction },
        {
          title: "Đánh giá cán bộ",
          href: "/evaluations",
          icon: "bi-file-earmark-text",
          check: () => hasPermission("evaluation.self") || hasPermission("evaluation.read"),
        },
        {
          title: "Đánh giá tập thể",
          href: "/collective-evaluations",
          icon: "bi-people",
          check: () =>
            hasPermission("evaluation.read") ||
            hasPermission("collective.manage") ||
            hasPermission("meeting.read") ||
            hasPermission("meeting.manage"),
        },
        { title: "Kết quả đánh giá", href: "/results", icon: "bi-trophy", permission: "evaluation.results.view" },
        { title: "Báo cáo", href: "/reports", icon: "bi-bar-chart-line", permission: "report.export" },
      ],
    },
    {
      title: "Quản lý",
      items: [
        { title: "Kỳ đánh giá", href: "/periods", icon: "bi-calendar3", permission: "period.manage" },
        { title: "Bộ tiêu chí", href: "/criteria", icon: "bi-list-check", permission: "criteria.manage" },
        { title: "Người dùng", href: "/admin/users", icon: "bi-people", permission: "system.users.read" },
        { title: "Vai trò", href: "/admin/roles", icon: "bi-shield-lock", permission: "system.roles.manage" },
        { title: "Danh mục", href: "/catalog", icon: "bi-building", permission: "catalog.manage" },
        { title: "Nhật ký", href: "/audit", icon: "bi-clock-history", permission: "system.audit.read" },
        { title: "Thông tin đơn vị", href: "/admin/settings", icon: "bi-gear", permission: "system.settings.manage" },
        { title: "Biểu mẫu Word", href: "/admin/templates", icon: "bi-file-earmark-word", permission: "system.templates.manage" },
      ],
    },
  ];

  const isVisible = (item: MenuItem) => {
    if (item.check) return item.check();
    if (item.permission) return hasPermission(item.permission);
    return true;
  };

  const visibleGroups = menuGroups
    .map((group) => ({ ...group, items: group.items.filter(isVisible) }))
    .filter((group) => group.items.length > 0);

  const closeOnMobile = () => {
    if (typeof window !== "undefined" && window.innerWidth < 768) closeSidebar();
  };

  return (
    <>
      {isSidebarOpen && <div onClick={closeSidebar} className="side-backdrop d-md-none" />}

      <aside className={`app-sidebar ${isSidebarOpen ? "sidebar-visible" : "sidebar-hidden"}`}>
        <div className="side-brand">
          <Emblem />
          <div className="side-brand-text">
            {brandSub && <div className="t1">{brandSub}</div>}
            <div className="t2">{brandMain}</div>
          </div>
          <button type="button" onClick={closeSidebar} className="side-close d-md-none" aria-label="Đóng menu">
            <i className="bi bi-x-lg" />
          </button>
        </div>

        {showPeriodPicker && (
          <div className="side-period">
            <label htmlFor="global-period">Kỳ đánh giá</label>
            <select
              id="global-period"
              value={periodId}
              onChange={(e) => setPeriodId(e.target.value)}
              disabled={periods.length === 0}
            >
              {periods.length === 0 && <option value="">Chưa có kỳ</option>}
              {periods.map((p) => (
                <option key={p.id} value={p.id} title={p.name}>
                  Quý {["I", "II", "III", "IV"][p.quarter - 1] ?? p.quarter}/{p.year}
                  {p.status === "Open" ? "" : ` (${p.statusDisplayName})`}
                </option>
              ))}
            </select>
          </div>
        )}

        <nav className="side-nav">
          {visibleGroups.map((group, groupIndex) => (
            <React.Fragment key={group.title ?? `group-${groupIndex}`}>
              {group.title && <div className="side-grp">{group.title}</div>}
              {group.items.map((item) => {
                const isActive =
                  pathname === item.href || (item.href !== "/" && pathname.startsWith(`${item.href}/`));
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={closeOnMobile}
                    className={isActive ? "on" : undefined}
                    aria-current={isActive ? "page" : undefined}
                  >
                    <i className={`bi ${item.icon}`} />
                    <span>{item.title}</span>
                  </Link>
                );
              })}
            </React.Fragment>
          ))}
        </nav>

        <AccountMenu onNavigate={closeOnMobile} />
      </aside>
    </>
  );
}

export default AppSidebar;
