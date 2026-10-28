"use client";

import React, { createContext, useContext, useState } from "react";

const TitleValue = createContext<string | null>(null);
const TitleSetter = createContext<React.Dispatch<React.SetStateAction<string | null>> | null>(null);

/** Tiêu đề trang do PageHeader đăng ký, hiển thị ở thanh trên. */
export function PageTitleProvider({ children }: { children: React.ReactNode }) {
  const [title, setTitle] = useState<string | null>(null);
  return (
    <TitleSetter.Provider value={setTitle}>
      <TitleValue.Provider value={title}>{children}</TitleValue.Provider>
    </TitleSetter.Provider>
  );
}

export function usePageTitle(): string | null {
  return useContext(TitleValue);
}

/** null khi không có khung (PageHeader tự hiện tiêu đề trong thân trang). */
export function usePageTitleSetter() {
  return useContext(TitleSetter);
}
