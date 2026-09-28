"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";

export function AppSidebar() {
  const pathname = usePathname();
  const { user, logout, hasPermission } = useAuth();
  const { isSidebarOpen, closeSidebar } = useLayout();

  const allMenuItems = [
    { title: "Tổng quan", href: "/", icon: "bi-grid-1x2-fill" },
    {
      title: "Đánh giá cán bộ",
      href: "/evaluations",
      icon: "bi-check2-square",
      check: () =>
        hasPermission("evaluation.self") ||
        hasPermission("evaluation.read"),
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
    {
      title: "Cán bộ & Tổ chức",
      href: "/users",
      icon: "bi-people-fill",
      check: () =>
        hasPermission("system.users.read") ||
        hasPermission("catalog.manage") ||
        hasPermission("system.assignments.manage") ||
        hasPermission("system.roles.manage"),
    },
    {
      title: "Tài liệu đính kèm",
      href: "/attachments",
      icon: "bi-folder2-open",
      // Mọi người đã đăng nhập (quyền trên tệp = quyền trên hồ sơ gắn tệp, máy chủ kiểm tra)
    },
    {
      title: "Báo cáo",
      href: "/reports",
      icon: "bi-bar-chart-line-fill",
      permission: "report.export",
    },
    {
      title: "Nhật ký hệ thống",
      href: "/audit",
      icon: "bi-clock-history",
      permission: "system.audit.read",
    },
    { title: "Biểu mẫu", href: "/forms", icon: "bi-file-earmark-text-fill" },
    { title: "Danh mục tổ chức", href: "/catalog", icon: "bi-building", permission: "catalog.manage" },
    { title: "Nhập dữ liệu", href: "/imports", icon: "bi-file-earmark-arrow-up-fill", permission: "system.import" },
  ];

  const menuItems = allMenuItems.filter((item) => {
    if (item.check) return item.check();
    if (item.permission) return hasPermission(item.permission);
    return true;
  });

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
            <div className="sidebar-brand-text">Đảng bộ ATTECH</div>
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
          <ul className="list-unstyled m-0 p-0">
            {menuItems.map((item) => {
              const isActive =
                pathname === item.href ||
                (item.href !== "/" && pathname.startsWith(item.href));
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
        </nav>

        {/* User Footer */}
        <div className="sidebar-footer">
          {user ? (
            <>
              <div className="user-avatar">{initials}</div>
              <div style={{ minWidth: 0, flex: 1 }}>
                <div className="user-name text-truncate">{user.fullName}</div>
                <div className="user-role text-truncate">{user.roles?.[0] || "Cán bộ"}</div>
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
