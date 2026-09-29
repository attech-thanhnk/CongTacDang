"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { sessionRoleLabel, useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { useOrganizationInfo } from "@/services/organizationSettingsService";

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

export function AppSidebar() {
  const pathname = usePathname();
  const { user, logout, hasPermission } = useAuth();
  const { isSidebarOpen, closeSidebar } = useLayout();
  const org = useOrganizationInfo();

  // Menu chỉ hiện theo mã quyền — máy chủ luôn kiểm tra lại, kể cả phạm vi.
  const hasAnyEvaluationAction = () =>
    (user?.permissions ?? []).some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");

  const menuGroups: MenuGroup[] = [
    {
      items: [
        { title: "Tổng quan", href: "/", icon: "bi-grid-1x2-fill" },
        { title: "Việc cần xử lý", href: "/work-queue", icon: "bi-inbox-fill", check: hasAnyEvaluationAction },
        {
          title: "Đánh giá cán bộ",
          href: "/evaluations",
          icon: "bi-check2-square",
          check: () => hasPermission("evaluation.self") || hasPermission("evaluation.read"),
        },
        {
          title: "Đánh giá tập thể",
          href: "/collective-evaluations",
          icon: "bi-diagram-3-fill",
          check: () =>
            hasPermission("evaluation.read") ||
            hasPermission("collective.manage") ||
            hasPermission("meeting.read") ||
            hasPermission("meeting.manage"),
        },
        { title: "Kỳ đánh giá", href: "/periods", icon: "bi-calendar-range", permission: "period.manage" },
        { title: "Bộ tiêu chí", href: "/criteria", icon: "bi-list-check", permission: "criteria.manage" },
        { title: "Báo cáo", href: "/reports", icon: "bi-bar-chart-line-fill", permission: "report.export" },
        { title: "Biểu mẫu", href: "/forms", icon: "bi-file-earmark-text-fill" },
      ],
    },
    {
      title: "Quản trị",
      items: [
        { title: "Tài khoản", href: "/admin/users", icon: "bi-people-fill", permission: "system.users.read" },
        { title: "Vai trò", href: "/admin/roles", icon: "bi-shield-lock-fill", permission: "system.roles.manage" },
        { title: "Danh mục", href: "/catalog", icon: "bi-building", permission: "catalog.manage" },
        { title: "Nhật ký", href: "/audit", icon: "bi-clock-history", permission: "system.audit.read" },
        { title: "Thông tin đơn vị", href: "/admin/settings", icon: "bi-gear-fill", permission: "system.settings.manage" },
        { title: "Biểu mẫu Word", href: "/admin/templates", icon: "bi-file-earmark-word-fill", permission: "system.templates.manage" },
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

  // Lấy chữ cái đầu của tên để làm avatar
  const initials = user?.fullName
    ? user.fullName
        .split(" ")
        .slice(-2)
        .map((w) => w[0])
        .join("")
        .toUpperCase()
    : "U";

  return (
    <>
      {/* Backdrop Mobile */}
      {isSidebarOpen && (
        <div
          onClick={closeSidebar}
          className="d-md-none position-fixed top-0 start-0 w-100 h-100"
          style={{ zIndex: 1040, backgroundColor: "rgba(0,0,0,0.45)", backdropFilter: "blur(3px)" }}
        />
      )}

      <aside
        className={`app-sidebar ${isSidebarOpen ? "sidebar-visible" : "sidebar-hidden"}`}
      >
        {/* Brand */}
        <div className="sidebar-brand">
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="sidebar-brand-text">{org?.systemName ?? "Đánh giá cán bộ"}</div>
            <div className="sidebar-brand-sub">Hệ thống đánh giá cán bộ</div>
          </div>

          {/* Close trên mobile */}
          <button
            type="button"
            onClick={closeSidebar}
            className="d-md-none btn p-0"
            style={{
              background: "transparent",
              border: "none",
              color: "rgba(255,255,255,0.5)",
              fontSize: "18px",
              lineHeight: 1,
              cursor: "pointer",
            }}
            aria-label="Đóng menu"
          >
            <i className="bi bi-x-lg" />
          </button>
        </div>

        {/* Navigation */}
        <nav className="sidebar-nav">
          {visibleGroups.map((group, groupIndex) => (
            <React.Fragment key={group.title ?? `group-${groupIndex}`}>
              {group.title && (
                <div
                  className="px-3 pt-3 pb-1 text-uppercase fw-semibold"
                  style={{ fontSize: "11px", letterSpacing: "0.06em", color: "rgba(255,255,255,0.4)" }}
                >
                  {group.title}
                </div>
              )}
              <ul className="list-unstyled m-0 p-0">
                {group.items.map((item) => {
                  const isActive =
                    pathname === item.href || (item.href !== "/" && pathname.startsWith(`${item.href}/`));
                  return (
                    <li key={item.href}>
                      <Link
                        href={item.href}
                        onClick={() => {
                          if (typeof window !== "undefined" && window.innerWidth < 768) {
                            closeSidebar();
                          }
                        }}
                        className={`nav-link ${isActive ? "active" : ""}`}
                      >
                        <i className={`bi ${item.icon}`} />
                        <span>{item.title}</span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </React.Fragment>
          ))}
        </nav>

        {/* User Footer */}
        <div className="sidebar-footer">
          {user ? (
            <>
              <div className="user-avatar">{initials}</div>
              <div style={{ minWidth: 0, flex: 1 }}>
                <div className="user-name text-truncate">{user.fullName}</div>
                <div className="user-role text-truncate">{sessionRoleLabel(user)}</div>
              </div>
              <button
                type="button"
                onClick={() => logout()}
                title="Đăng xuất"
                style={{
                  background: "transparent",
                  border: "none",
                  color: "rgba(255,255,255,0.45)",
                  cursor: "pointer",
                  fontSize: "14px",
                  padding: "4px",
                  borderRadius: "4px",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  transition: "color var(--transition-fast)",
                }}
                onMouseEnter={(e) => (e.currentTarget.style.color = "#ff4d4f")}
                onMouseLeave={(e) => (e.currentTarget.style.color = "rgba(255,255,255,0.45)")}
                aria-label="Đăng xuất"
              >
                <i className="bi bi-box-arrow-right" />
              </button>
            </>
          ) : (
            <div style={{ color: "rgba(255,255,255,0.3)", fontSize: "12px" }}>Chưa đăng nhập</div>
          )}
        </div>
      </aside>
    </>
  );
}

export default AppSidebar;
