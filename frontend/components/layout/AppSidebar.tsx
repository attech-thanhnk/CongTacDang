"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useLayout } from "@/contexts/LayoutContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { useOrganizationInfo } from "@/services/organizationSettingsService";
import { AccountMenu } from "./AccountMenu";
import { buildMenu, isMenuItemActive } from "./menu";

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

const COLLAPSED_KEY = "sidebar-collapsed";

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

  const visibleGroups = buildMenu(user, hasPermission);
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  // Nhớ nhóm đã gập; mặc định gập nhóm Hệ thống, trừ người chỉ làm quản trị hệ thống.
  const onlySystemWork =
    !!user && !user.permissions.some((code) => /^(evaluation.|period.|criteria.)/.test(code));
  useEffect(() => {
    const fallback = { system: !onlySystemWork };
    try {
      const saved = localStorage.getItem(COLLAPSED_KEY);
      setCollapsed(saved ? JSON.parse(saved) : fallback);
    } catch {
      setCollapsed(fallback);
    }
  }, [onlySystemWork]);

  const toggleGroup = (key: string) =>
    setCollapsed((prev) => {
      const next = { ...prev, [key]: !prev[key] };
      try {
        localStorage.setItem(COLLAPSED_KEY, JSON.stringify(next));
      } catch {
        // Không lưu được thì chỉ nhớ trong phiên.
      }
      return next;
    });

  // Mục đang mở có thể nằm dưới vùng nhìn thấy của menu.
  useEffect(() => {
    document.querySelector(".side-nav a.on")?.scrollIntoView({ block: "nearest" });
  }, [pathname]);

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
          {visibleGroups.map((group) => {
            const hasActive = group.items.some((item) => isMenuItemActive(item, pathname));
            // Nhóm chứa trang đang mở luôn mở.
            const isOpen = !group.collapsible || hasActive || !collapsed[group.key];
            return (
              <React.Fragment key={group.key}>
                {group.title &&
                  (group.collapsible ? (
                    <button
                      type="button"
                      className="side-grp side-grp-toggle"
                      aria-expanded={isOpen}
                      onClick={() => toggleGroup(group.key)}
                      disabled={hasActive}
                    >
                      <span>{group.title}</span>
                      <i className={`bi ${isOpen ? "bi-chevron-up" : "bi-chevron-down"}`} aria-hidden="true" />
                    </button>
                  ) : (
                    <div className="side-grp">{group.title}</div>
                  ))}
                {isOpen &&
                  group.items.map((item) => {
                    const isActive = isMenuItemActive(item, pathname);
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
            );
          })}
        </nav>

        <AccountMenu onNavigate={closeOnMobile} />
      </aside>
    </>
  );
}

export default AppSidebar;
