"use client";

import React from "react";
import { createPortal } from "react-dom";
import { useActionsSlot } from "@/contexts/PageTitleContext";
import { usePhone } from "./usePhone";

/** Đưa nút thao tác của một phần trang (ví dụ một thẻ tab) lên thanh trên; điện thoại hoặc không có thanh trên thì để tại chỗ. */
export function TopBarActions({ children }: { children: React.ReactNode }) {
  const slot = useActionsSlot();
  const phone = usePhone();
  const inline = <div className="d-flex justify-content-end mb-2">{children}</div>;
  if (!slot || phone) return inline;
  return createPortal(<div className="page-header-actions">{children}</div>, slot);
}

export default TopBarActions;
