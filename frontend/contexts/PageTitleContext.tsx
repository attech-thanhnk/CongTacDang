"use client";

import React, { createContext, useContext, useState } from "react";

const TitleValue = createContext<string | null>(null);
const ActionsSlot = createContext<HTMLElement | null>(null);
const ActionsSlotSetter = createContext<((el: HTMLElement | null) => void) | null>(null);
const TitleSetter = createContext<React.Dispatch<React.SetStateAction<string | null>> | null>(null);

/** Tiêu đề trang do PageHeader đăng ký, hiển thị ở thanh trên. */
export function PageTitleProvider({ children }: { children: React.ReactNode }) {
  const [title, setTitle] = useState<string | null>(null);
  const [slot, setSlot] = useState<HTMLElement | null>(null);
  return (
    <TitleSetter.Provider value={setTitle}>
      <ActionsSlotSetter.Provider value={setSlot}>
        <ActionsSlot.Provider value={slot}>
          <TitleValue.Provider value={title}>{children}</TitleValue.Provider>
        </ActionsSlot.Provider>
      </ActionsSlotSetter.Provider>
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

/** Vùng trên thanh trên nhận nút thao tác chính của trang (PageHeader đưa vào bằng portal). */
export function useActionsSlot() {
  return useContext(ActionsSlot);
}

export function useActionsSlotSetter() {
  return useContext(ActionsSlotSetter);
}
