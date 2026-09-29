"use client";

import React, { useState } from "react";
import { sessionRoleLabel, useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { useToast } from "@/contexts/ToastContext";
import { usePathname } from "next/navigation";
import { useOrganizationInfo } from "@/services/organizationSettingsService";

export function AppHeader() {
  const { user, logout, loading } = useAuth();
  const { toggleSidebar } = useLayout();
  const { toast } = useToast();
  const pathname = usePathname();
  const org = useOrganizationInfo();
  const [userDropdownOpen, setUserDropdownOpen] = useState(false);

  const getPageTitle = () => {
    if (pathname === "/") return "Tổng quan";
    if (pathname.startsWith("/work-queue")) return "Việc cần xử lý";
    if (pathname.startsWith("/evaluations")) return "Đánh giá cán bộ";
    if (pathname.startsWith("/collective-evaluations")) return "Đánh giá tập thể";
    if (pathname.startsWith("/periods")) return "Kỳ đánh giá";
    if (pathname.startsWith("/admin/users")) return "Tài khoản";
    if (pathname.startsWith("/admin/roles")) return "Vai trò";
    if (pathname.startsWith("/catalog")) return "Danh mục";
    if (pathname.startsWith("/imports")) return "Nhập dữ liệu";
    if (pathname.startsWith("/audit")) return "Nhật ký";
    if (pathname.startsWith("/attachments")) return "Tài liệu đính kèm";
    if (pathname.startsWith("/reports")) return "Báo cáo";
    if (pathname.startsWith("/forms")) return "Biểu mẫu";
    return "";
  };

  const handleLogoutClick = async () => {
    setUserDropdownOpen(false);
    try {
      await logout();
    } catch (err: any) {
      toast.error(err.message || "Đăng xuất thất bại.");
    }
  };

  const initials = user?.fullName
    ? user.fullName
        .split(" ")
        .slice(-2)
        .map((w) => w[0])
        .join("")
        .toUpperCase()
    : "U";

  return (
    <header className="app-header">
      {/* Left */}
      <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
        <button
          type="button"
          onClick={toggleSidebar}
          style={{
            width: "32px",
            height: "32px",
            background: "transparent",
            border: "none",
            borderRadius: "8px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            cursor: "pointer",
            color: "var(--text-secondary)",
            transition: "background var(--transition-fast)",
            flexShrink: 0,
          }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "var(--bg-row-hover)")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
          title="Ẩn/hiện menu"
          aria-label="Ẩn/hiện menu"
        >
          <i className="bi bi-list" style={{ fontSize: "18px" }} />
        </button>

        <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
          <span className="header-title" style={{ fontSize: "13px", fontWeight: 600 }}>{org?.systemName ?? "Đánh giá cán bộ"}</span>
          {pathname !== "/" && (
            <>
              <span className="header-sep">/</span>
              <span className="header-page">{getPageTitle()}</span>
            </>
          )}
        </div>
      </div>

      {/* Right */}
      <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
        {loading ? (
          <span style={{ fontSize: "12px", color: "var(--text-muted)" }}>Đang nạp...</span>
        ) : user ? (
          <div style={{ position: "relative" }}>
            <button
              type="button"
              onClick={() => setUserDropdownOpen(!userDropdownOpen)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "8px",
                padding: "4px 10px 4px 4px",
                background: "var(--bg-app)",
                border: "1px solid var(--border-base)",
                borderRadius: "24px",
                cursor: "pointer",
                transition: "all var(--transition-fast)",
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.borderColor = "var(--border-strong)";
                e.currentTarget.style.boxShadow = "var(--shadow-xs)";
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.borderColor = "var(--border-base)";
                e.currentTarget.style.boxShadow = "none";
              }}
            >
              {/* Mini avatar */}
              <div
                style={{
                  width: "28px",
                  height: "28px",
                  borderRadius: "50%",
                  background: "linear-gradient(135deg, var(--color-cobalt), var(--color-teal))",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#fff",
                  flexShrink: 0,
                }}
              >
                {initials}
              </div>

              <div
                className="d-none d-sm-flex flex-column text-start"
                style={{ lineHeight: 1.15, gap: "2px" }}
              >
                <span style={{ fontSize: "13px", fontWeight: 600, color: "var(--text-primary)" }}>
                  {user.fullName}
                </span>
                <span style={{ fontSize: "11.5px", color: "var(--text-secondary)" }}>
                  {sessionRoleLabel(user)}
                </span>
              </div>

              <i
                className="bi bi-chevron-down"
                style={{ fontSize: "11px", color: "var(--text-secondary)" }}
              />
            </button>

            {/* Dropdown */}
            {userDropdownOpen && (
              <>
                <div
                  onClick={() => setUserDropdownOpen(false)}
                  style={{
                    position: "fixed",
                    inset: 0,
                    zIndex: 1040,
                  }}
                />
                <div
                  style={{
                    position: "absolute",
                    right: 0,
                    top: "calc(100% + 8px)",
                    width: "220px",
                    background: "var(--bg-card)",
                    border: "1px solid var(--border-base)",
                    borderRadius: "var(--radius-lg)",
                    boxShadow: "var(--shadow-lg)",
                    zIndex: 1050,
                    overflow: "hidden",
                  }}
                >
                  {/* User info header */}
                  <div
                    style={{
                      padding: "14px 16px",
                      borderBottom: "1px solid var(--border-subtle)",
                      background: "var(--bg-app)",
                    }}
                  >
                    <div
                      style={{
                        fontWeight: 600,
                        fontSize: "13px",
                        color: "var(--text-primary)",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {user.fullName}
                    </div>
                    <div
                      style={{
                        fontSize: "11px",
                        color: "var(--text-muted)",
                        marginTop: "2px",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {user.userName}
                    </div>
                  </div>

                  {/* Actions */}
                  <div style={{ padding: "6px" }}>
                    <button
                      type="button"
                      onClick={handleLogoutClick}
                      style={{
                        width: "100%",
                        display: "flex",
                        alignItems: "center",
                        gap: "8px",
                        padding: "8px 10px",
                        background: "transparent",
                        border: "none",
                        borderRadius: "var(--radius-sm)",
                        cursor: "pointer",
                        fontSize: "13px",
                        color: "var(--color-danger)",
                        fontWeight: 500,
                        transition: "background var(--transition-fast)",
                      }}
                      onMouseEnter={(e) =>
                        (e.currentTarget.style.background = "var(--color-danger-bg)")
                      }
                      onMouseLeave={(e) =>
                        (e.currentTarget.style.background = "transparent")
                      }
                    >
                      <i className="bi bi-box-arrow-right" />
                      <span>Đăng xuất</span>
                    </button>
                  </div>
                </div>
              </>
            )}
          </div>
        ) : null}
      </div>
    </header>
  );
}

export default AppHeader;
