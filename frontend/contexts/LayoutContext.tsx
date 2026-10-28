"use client";

import React, { createContext, useCallback, useContext, useState } from "react";

interface LayoutContextType {
  /** Màn hẹp (< 768px): thanh bên đang trượt ra. */
  isSidebarOpen: boolean;
  /** Màn rộng: thanh bên thu thành cột biểu tượng. */
  isSidebarCollapsed: boolean;
  /** Màn hẹp thì trượt ra/vào, màn rộng thì thu gọn/mở rộng. */
  toggleSidebar: () => void;
  closeSidebar: () => void;
}

const LayoutContext = createContext<LayoutContextType | undefined>(undefined);

const COLLAPSED_KEY = "ctd.sidebarCollapsed";
const DESKTOP_MIN = 768;
const WIDE_MIN = 1280;

/** Lần đầu (chưa có lựa chọn đã lưu) thì thu gọn khi chiều ngang dưới 1280px. */
function initialCollapsed(): boolean {
  if (typeof window === "undefined") return false;
  try {
    const saved = window.localStorage.getItem(COLLAPSED_KEY);
    if (saved === "1") return true;
    if (saved === "0") return false;
  } catch {
    // Trình duyệt chặn lưu trữ: dùng mặc định theo chiều ngang.
  }
  return window.innerWidth < WIDE_MIN;
}

export function LayoutProvider({ children }: { children: React.ReactNode }) {
  // Provider chỉ dựng ở phía trình duyệt sau khi nạp phiên nên đọc window ngay ở khởi tạo là an toàn.
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isSidebarCollapsed, setCollapsed] = useState(initialCollapsed);

  const toggleSidebar = useCallback(() => {
    if (window.innerWidth < DESKTOP_MIN) {
      setIsSidebarOpen((prev) => !prev);
      return;
    }
    setCollapsed((prev) => {
      const next = !prev;
      try {
        window.localStorage.setItem(COLLAPSED_KEY, next ? "1" : "0");
      } catch {
        // Không lưu được thì chỉ nhớ trong phiên.
      }
      return next;
    });
  }, []);

  const closeSidebar = useCallback(() => setIsSidebarOpen(false), []);

  return (
    <LayoutContext.Provider value={{ isSidebarOpen, isSidebarCollapsed, toggleSidebar, closeSidebar }}>
      {children}
    </LayoutContext.Provider>
  );
}

export function useLayout() {
  const context = useContext(LayoutContext);
  if (!context) {
    throw new Error("useLayout must be used within a LayoutProvider");
  }
  return context;
}
