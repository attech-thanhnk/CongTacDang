"use client";

import React from "react";

/** Đầu thẻ nội dung chính: bộ lọc/ô tìm bên trái, nút thao tác chính căn phải, cùng một hàng. */
/** `alignEnd`: bộ lọc có nhãn trên ô nhập — nút căn đáy để cùng tâm với ô nhập, không theo nhãn. */
export function CardToolbar({ children, actions, alignEnd = false }: { children?: React.ReactNode; actions?: React.ReactNode; alignEnd?: boolean }) {
  if (!children && !actions) return null;
  return (
    <div className={`card-header card-toolbar${alignEnd ? " card-toolbar-end" : ""}`}>
      {children && <div className="card-toolbar-main">{children}</div>}
      {actions && <div className="card-toolbar-actions">{actions}</div>}
    </div>
  );
}

/** Hàng tab có nút thao tác chính ở mép phải, không tạo hàng riêng cho nút. */
export function TabsRow({ children, actions }: { children: React.ReactNode; actions?: React.ReactNode }) {
  return (
    <div className="tabs-row">
      {children}
      {actions && <div className="tabs-row-actions">{actions}</div>}
    </div>
  );
}

/** Thanh hành động dính đáy khung nội dung cho form dài (Lưu/Hủy, chỉ báo chưa lưu). */
export function StickyActionBar({ children }: { children: React.ReactNode }) {
  return <div className="sticky-action-bar">{children}</div>;
}
