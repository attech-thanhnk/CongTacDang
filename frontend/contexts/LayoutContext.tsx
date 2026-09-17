"use client";

import React, { createContext, useContext, useState, useEffect } from "react";

interface LayoutContextType {
  isSidebarOpen: boolean;
  toggleSidebar: () => void;
  closeSidebar: () => void;
}

const LayoutContext = createContext<LayoutContextType | undefined>(undefined);

export function LayoutProvider({ children }: { children: React.ReactNode }) {
  // Mặc định: mở trên desktop (>= 768px), đóng trên mobile
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);

  useEffect(() => {
    // Nếu màn hình nhỏ hơn 768px thì mặc định ẩn
    if (typeof window !== "undefined" && window.innerWidth < 768) {
      setIsSidebarOpen(false);
    }
  }, []);

  const toggleSidebar = () => {
    setIsSidebarOpen((prev) => !prev);
  };

  const closeSidebar = () => {
    setIsSidebarOpen(false);
  };

  return (
    <LayoutContext.Provider
      value={{
        isSidebarOpen,
        toggleSidebar,
        closeSidebar,
      }}
    >
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
