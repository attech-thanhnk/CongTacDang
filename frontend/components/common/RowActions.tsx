"use client";

import React, { useCallback, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import styles from "./RowActions.module.css";

export interface RowAction {
  key: string;
  label: string;
  /** Lớp bootstrap-icons, ví dụ "bi-pencil-square". */
  icon?: string;
  onClick: () => void;
  /** Thao tác nguy hiểm: luôn nằm cuối menu, chữ đỏ, có đường kẻ ngăn. */
  danger?: boolean;
  disabled?: boolean;
  /** Ẩn khi không đủ điều kiện (giữ khai báo gọn ở nơi dùng). */
  hidden?: boolean;
  /** Chú thích khi rê chuột, mặc định là label. */
  title?: string;
}

export interface RowActionsProps {
  /** Nút hiển thị trực tiếp (nên 0–2). */
  primary?: RowAction[];
  /** Thao tác gom vào menu "⋯". */
  menu?: RowAction[];
  /** Tên đối tượng của dòng, dùng cho nhãn đọc màn hình của nút menu. */
  itemLabel?: string;
  /** Nút chính chỉ hiện icon (nhãn vẫn có trong aria-label/title). */
  iconOnly?: boolean;
  /** Kiểu nút chính theo thứ tự: "primary" tô màu, còn lại viền nhạt. */
  primaryVariant?: Array<"primary" | "outline">;
  disabled?: boolean;
}

const MENU_GAP = 4;

/** Cột "Thao tác" của bảng: vài nút chính + menu ⋯ (vẽ ra body để không bị khung bảng cắt). */
export function RowActions({ primary = [], menu = [], itemLabel, iconOnly, primaryVariant, disabled }: RowActionsProps) {
  const shown = primary.filter((a) => !a.hidden);
  const items = menu.filter((a) => !a.hidden);
  // Thao tác nguy hiểm luôn xuống cuối để tránh bấm nhầm.
  const normal = items.filter((a) => !a.danger);
  const danger = items.filter((a) => a.danger);
  const ordered = [...normal, ...danger];

  const [open, setOpen] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number; up: boolean } | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const menuId = useId();

  const close = useCallback((focusTrigger: boolean) => {
    setOpen(false);
    setPos(null);
    if (focusTrigger) triggerRef.current?.focus();
  }, []);

  const place = useCallback(() => {
    const trigger = triggerRef.current;
    const panel = menuRef.current;
    if (!trigger || !panel) return;
    const r = trigger.getBoundingClientRect();
    const w = panel.offsetWidth;
    const h = panel.offsetHeight;
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const up = r.bottom + MENU_GAP + h > vh && r.top - MENU_GAP - h > 0;
    const top = up ? r.top - MENU_GAP - h : r.bottom + MENU_GAP;
    const left = Math.max(8, Math.min(r.right - w, vw - w - 8));
    setPos({ top, left, up });
  }, []);

  useLayoutEffect(() => {
    if (open) place();
  }, [open, place]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent | TouchEvent) => {
      const t = e.target as Node;
      if (menuRef.current?.contains(t) || triggerRef.current?.contains(t)) return;
      close(false);
    };
    // Menu đặt cố định nên đóng khi cuộn/đổi cỡ để không trôi khỏi dòng.
    const onScroll = (e: Event) => {
      if (menuRef.current?.contains(e.target as Node)) return;
      close(false);
    };
    const onResize = () => close(false);
    document.addEventListener("mousedown", onDown);
    document.addEventListener("touchstart", onDown);
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", onResize);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("touchstart", onDown);
      window.removeEventListener("scroll", onScroll, true);
      window.removeEventListener("resize", onResize);
    };
  }, [open, close]);

  useEffect(() => {
    if (open && pos) {
      const first = menuRef.current?.querySelector<HTMLButtonElement>("button:not(:disabled)");
      first?.focus();
    }
    // Chỉ lấy tiêu điểm lần đầu khi menu vừa định vị.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, pos === null]);

  const focusItem = (dir: 1 | -1 | "first" | "last") => {
    const buttons = Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>("button:not(:disabled)") ?? []);
    if (buttons.length === 0) return;
    const idx = buttons.indexOf(document.activeElement as HTMLButtonElement);
    let next = 0;
    if (dir === "first") next = 0;
    else if (dir === "last") next = buttons.length - 1;
    else next = (idx + dir + buttons.length) % buttons.length;
    buttons[next].focus();
  };

  const onMenuKey = (e: React.KeyboardEvent) => {
    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        focusItem(1);
        break;
      case "ArrowUp":
        e.preventDefault();
        focusItem(-1);
        break;
      case "Home":
        e.preventDefault();
        focusItem("first");
        break;
      case "End":
        e.preventDefault();
        focusItem("last");
        break;
      case "Escape":
        e.preventDefault();
        close(true);
        break;
      case "Tab":
        close(false);
        break;
    }
  };

  const onTriggerKey = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      setOpen(true);
    }
  };

  const run = (action: RowAction) => {
    close(true);
    action.onClick();
  };

  const renderItem = (a: RowAction) => (
    <button
      key={a.key}
      type="button"
      role="menuitem"
      className={`${styles.item} ${a.danger ? styles.danger : ""}`}
      disabled={disabled || a.disabled}
      title={a.title && a.title !== a.label ? a.title : undefined}
      onClick={() => run(a)}
    >
      {a.icon ? <i className={`bi ${a.icon} ${styles.itemIcon}`} aria-hidden="true" /> : <span className={styles.itemIcon} />}
      <span>{a.label}</span>
    </button>
  );

  return (
    <div className={styles.wrap}>
      {shown.map((a, i) => {
        const variant = primaryVariant?.[i] === "primary" ? "btn-primary" : "btn-outline-secondary";
        return (
          <button
            key={a.key}
            type="button"
            className={`btn btn-sm ${variant} ${styles.primaryBtn} ${iconOnly ? styles.iconOnly : ""} ${a.danger ? styles.primaryDanger : ""}`}
            aria-label={a.label}
            title={a.title ?? a.label}
            disabled={disabled || a.disabled}
            onClick={a.onClick}
          >
            {a.icon && <i className={`bi ${a.icon}`} aria-hidden="true" />}
            {!iconOnly && <span className={styles.primaryLabel}>{a.label}</span>}
          </button>
        );
      })}
      {ordered.length > 0 && (
        <button
          ref={triggerRef}
          type="button"
          className={`btn btn-sm ${styles.more} ${open ? styles.moreOpen : ""}`}
          aria-label={itemLabel ? `Thao tác khác — ${itemLabel}` : "Thao tác khác"}
          title="Thao tác khác"
          aria-haspopup="menu"
          aria-expanded={open}
          aria-controls={open ? menuId : undefined}
          disabled={disabled}
          onClick={() => (open ? close(false) : setOpen(true))}
          onKeyDown={onTriggerKey}
        >
          <i className="bi bi-three-dots" aria-hidden="true" />
        </button>
      )}
      {open &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            ref={menuRef}
            id={menuId}
            role="menu"
            aria-label={itemLabel ? `Thao tác — ${itemLabel}` : "Thao tác"}
            className={`${styles.menu} ${pos?.up ? styles.up : ""}`}
            style={pos ? { top: pos.top, left: pos.left } : { top: -9999, left: -9999, visibility: "hidden" }}
            onKeyDown={onMenuKey}
          >
            {normal.map(renderItem)}
            {normal.length > 0 && danger.length > 0 && <div className={styles.sep} role="separator" />}
            {danger.map(renderItem)}
          </div>,
          document.body
        )}
    </div>
  );
}

export default RowActions;
